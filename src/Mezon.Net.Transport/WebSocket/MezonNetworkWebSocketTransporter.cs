using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Transport.Internal;

namespace Mezon.Net.Transport
{
    public class MezonNetworkWebSocketTransporter : IMezonNetworkTransporter, IDisposable, IAsyncDisposable
    {
        private const string TokenHeaderKey = "token";
        private const string DefaultLanguage = "en";
        private const int WsReceiveBufferSize = 8192;

        private ConnectionState _state = ConnectionState.Disconnected;
        private ClientWebSocket? _wsClient;
        private CancellationToken _externalCt, _internalCt;
        private IDictionary<string, string>? _headers;
        private readonly ConcurrentDictionary<int, ArrayBufferWriter<byte>> _apiChunkBuffers = new ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        private CancellationTokenSource? _disconnectCts, _internalCts;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private Channel<ReadOnlyMemory<byte>>? _sendChannel;
        private bool _disposed;
        private int _connectionGeneration;

        public Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
        public Func<Task>? Opened { get; set; }
        public Func<Exception?, Task>? Closed { get; set; }
        public Func<Exception, Task>? ErrorOccurred { get; set; }

        public MezonNetworkWebSocketTransporter()
        {
            _disconnectCts = new CancellationTokenSource();
            _externalCt = CancellationToken.None;
            _internalCt = CancellationToken.None;
        }

        public void SetCancelToken(CancellationToken cancellationToken)
        {
            _internalCts?.Dispose();
            _externalCt = cancellationToken;
            _internalCts = CancellationTokenSource.CreateLinkedTokenSource(_externalCt, _disconnectCts?.Token ?? CancellationToken.None);
            _internalCt = _internalCts.Token;
        }

        public void SetHeader(IDictionary<string, string> headers) => _headers = headers;

        public async Task ConnectAsync(string host, int? port = 443, string? token = null, bool? useSsl = false, bool? createStatus = false)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await ConnectInternalAsync(host, port, token, useSsl, createStatus).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ErrorOccurred != null)
                {
                    await ErrorOccurred.Invoke(ex).ConfigureAwait(false);
                }

                await DisconnectInternalAsync(invokeClosed: false).ConfigureAwait(false);
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task ConnectInternalAsync(string host, int? port = 443, string? token = null, bool? useSsl = false, bool? createStatus = false)
        {
            await DisconnectInternalAsync().ConfigureAwait(false);
            _disconnectCts?.Dispose();
            _internalCts?.Dispose();

            _disconnectCts = new CancellationTokenSource();
            _internalCts = CancellationTokenSource.CreateLinkedTokenSource(_externalCt, _disconnectCts.Token);
            _internalCt = _internalCts.Token;

            _state = ConnectionState.Connecting;
            _wsClient = new ClientWebSocket
            {
                Options =
                {
                    KeepAliveInterval = TimeSpan.Zero,
                }
            };

#if NET5_0_OR_GREATER
            _wsClient.Options.RemoteCertificateValidationCallback = MezonNetworkSettings.DefaultValidateServerCertificate;
#endif

            if (_headers?.Count > 0)
            {
                foreach (var header in _headers)
                {
                    if (header.Value != null)
                    {
                        _wsClient.Options.SetRequestHeader(header.Key, header.Value);
                    }
                }
            }

            string? wsToken = token;
            if (string.IsNullOrEmpty(wsToken) && _headers != null && _headers.TryGetValue(TokenHeaderKey, out var tokenHeader))
            {
                wsToken = tokenHeader;
            }

            if (string.IsNullOrEmpty(wsToken))
            {
                if (ErrorOccurred != null)
                {
                    await ErrorOccurred.Invoke(new NetworkTransportUnauthorizationException()).ConfigureAwait(false);
                }

                throw new NetworkTransportUnauthorizationException();
            }

            var uri = BuildUri(host, port ?? 443, createStatus ?? false, wsToken, useSsl.HasValue && useSsl.Value);
            await _wsClient.ConnectAsync(uri, _internalCt).ConfigureAwait(false);

            if (Opened != null)
            {
                await Opened.Invoke().ConfigureAwait(false);
            }

            _sendChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            var generation = Interlocked.Increment(ref _connectionGeneration);
            _ = Task.Run(() => ReceiveLoopAsync(_internalCt, generation), _internalCt);
            _ = Task.Run(() => SendLoopAsync(_internalCt), _internalCt);

            _state = ConnectionState.Connected;
        }

        private static Uri BuildUri(string host, int port, bool createStatus, string token, bool useSsl)
        {
            var status = createStatus ? "true" : "false";
            var escapedToken = Uri.EscapeDataString(token);
            return new UriBuilder
            {
                Scheme = useSsl ? "wss" : "ws",
                Host = host,
                Port = port,
                Path = "/ws",
                Query = $"lang={DefaultLanguage}&status={status}&token={escapedToken}"
            }.Uri;
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken, int generation)
        {
            byte[]? wsBuffer = null;
            try
            {
                if (_wsClient == null)
                {
                    return;
                }

                wsBuffer = ArrayPool<byte>.Shared.Rent(WsReceiveBufferSize);
                var messageWriter = new ArrayBufferWriter<byte>(WsReceiveBufferSize);
                while (_wsClient.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    messageWriter.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _wsClient.ReceiveAsync(new ArraySegment<byte>(wsBuffer), cancellationToken).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        if (result.Count > 0)
                        {
                            messageWriter.Write(wsBuffer.AsSpan(0, result.Count));
                        }
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (MessageReceived != null && messageWriter.WrittenCount > 0)
                    {
                        if (MezonWebSocketFrameCodec.TryHandleMessage(
                                messageWriter.WrittenMemory,
                                _apiChunkBuffers,
                                out var type,
                                out var cid,
                                out var code,
                                out var payload))
                        {
                            await MessageReceived.Invoke(type, cid, code, payload).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (ErrorOccurred != null)
                {
                    await ErrorOccurred.Invoke(ex).ConfigureAwait(false);
                }
            }
            finally
            {
                if (wsBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(wsBuffer);
                }

                if (generation == _connectionGeneration && _state == ConnectionState.Connected)
                {
                    await DisconnectAsync().ConfigureAwait(false);
                }
            }
        }

        public ValueTask SendAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> data)
        {
            if (_wsClient?.State != WebSocketState.Open || _state != ConnectionState.Connected || _sendChannel == null)
            {
                return new ValueTask(Task.FromException(new InvalidOperationException(
                    $"Cannot send on WebSocket (wsState={_wsClient?.State}, transportState={_state}, sendChannel={_sendChannel != null}).")));
            }

            switch (type)
            {
                case MezonMessageType.Api:
                case MezonMessageType.Realtime:
                    return MezonWebSocketFrameCodec.TryQueueRawFrame(_sendChannel.Writer, data)
                        ? default
                        : new ValueTask(Task.FromException(new InvalidOperationException("Cannot queue message for sending.")));
                case MezonMessageType.Heartbeat:
                    return new ValueTask(Task.FromException(new InvalidOperationException("WebSocket heartbeat must be sent as a Ping envelope.")));
                default:
                    return new ValueTask(Task.FromException(new InvalidOperationException($"Unsupported WebSocket message type '{type}'.")));
            }
        }

        private async Task SendLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_wsClient == null || _sendChannel == null)
                {
                    if (ErrorOccurred != null)
                    {
                        await ErrorOccurred.Invoke(new InvalidOperationException("Connection is not established.")).ConfigureAwait(false);
                    }

                    return;
                }

                while (await _sendChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (_sendChannel.Reader.TryRead(out var msgSend))
                    {
                        try
                        {
                            if (_wsClient.State != WebSocketState.Open)
                            {
                                return;
                            }

                            await _wsClient.SendAsync(msgSend, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            MezonWebSocketFrameCodec.ReturnPooledSendBuffer(msgSend);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (ErrorOccurred != null)
                {
                    await ErrorOccurred.Invoke(ex).ConfigureAwait(false);
                }
            }
        }

        public async Task DisconnectAsync(int closeCode = 1000, string? reason = null)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisconnectInternalAsync(closeCode).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task DisconnectInternalAsync(int closeCode = 1000, string? reason = null, bool invokeClosed = true)
        {
            if (_state == ConnectionState.Disconnected || _state == ConnectionState.Disconnecting)
            {
                return;
            }

            _state = ConnectionState.Disconnecting;
            _sendChannel?.Writer.TryComplete();

            if (_disconnectCts != null)
            {
                try
                {
                    _disconnectCts.Cancel(false);
                    _disconnectCts.Dispose();
                    _disconnectCts = null;
                }
                catch
                {
                }
            }

            _internalCts?.Cancel();

            if (_wsClient != null)
            {
                try
                {
                    if (_wsClient.State == WebSocketState.Open)
                    {
                        await _wsClient.CloseOutputAsync((WebSocketCloseStatus)closeCode, reason ?? "Normal Closure.", CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                }

                try
                {
                    _wsClient.Dispose();
                }
                catch
                {
                }

                _wsClient = null;
            }

            _apiChunkBuffers.Clear();
            _state = ConnectionState.Disconnected;
            if (invokeClosed && Closed != null)
            {
                await Closed.Invoke(null).ConfigureAwait(false);
            }
        }

        /// <inheritdoc cref="MezonNetworkTransporterExtensions.RemoveApiChunkBuffer(IMezonNetworkTransporter, int)"/>
        public void RemoveApiChunkBuffer(int cid) => _apiChunkBuffers.TryRemove(cid, out _);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectInternalAsync().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _semaphore.Dispose();
                _disconnectCts?.Dispose();
                _internalCts?.Dispose();
                _wsClient?.Dispose();
                _apiChunkBuffers.Clear();
            }

            _disposed = true;
        }
    }
}
