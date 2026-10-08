using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
#if NET7_0_OR_GREATER
using System.Net;
#endif
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Transport.Internal;

namespace Mezon.Net.Transport
{
    /// <remarks>
    /// Same lifecycle invariants as <see cref="MezonNetworkTcpTransporter"/>: <see cref="Closed"/> is raised outside
    /// the semaphore, loops receive their connection's socket and channel as arguments (never the mutable fields), and
    /// both loops are joined before a connection is considered closed.
    /// </remarks>
    public class MezonNetworkWebSocketTransporter : IMezonNetworkTransporter, IDisposable, IAsyncDisposable
    {
        private const string TokenHeaderKey = "token";
        private const string DefaultLanguage = "en";
        private const int WsReceiveBufferSize = 8192;

        /// <summary>One WebSocket message carries either a realtime envelope or an API response chunk.</summary>
        private const int MaxWsMessageLen = MezonTransportFrameCodec.MaxApiResponseLen + MezonWebSocketFrameCodec.ApiHeaderLength;

        private ConnectionState _state = ConnectionState.Disconnected;
        private ClientWebSocket? _wsClient;
        private CancellationToken _externalCt = CancellationToken.None;
        private IDictionary<string, string>? _headers;
        private readonly ConcurrentDictionary<int, ArrayBufferWriter<byte>> _apiChunkBuffers = new ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        private CancellationTokenSource? _connectionCts;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private Channel<ReadOnlyMemory<byte>>? _sendChannel;
        private Task? _receiveLoopTask;
        private Task? _sendLoopTask;
        private bool _disposed;
        private int _connectionGeneration;

        public Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
        public Func<Task>? Opened { get; set; }
        public Func<Exception?, Task>? Closed { get; set; }
        public Func<Exception, Task>? ErrorOccurred { get; set; }

        /// <summary>Token linked into every subsequent connection; cancelling it aborts a connect in progress.</summary>
        public void SetCancelToken(CancellationToken cancellationToken)
        {
            _externalCt = cancellationToken;
        }

        public void SetHeader(IDictionary<string, string> headers) => _headers = headers;

        public async Task ConnectAsync(string host, int? port = 443, string? token = null, bool? useSsl = false, bool? createStatus = false)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MezonNetworkWebSocketTransporter));
            }

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

                await DisconnectInternalAsync().ConfigureAwait(false);
                await WaitForBackgroundLoopsAsync().ConfigureAwait(false);
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
            await WaitForBackgroundLoopsAsync().ConfigureAwait(false);
            _connectionCts?.Dispose();
            _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(_externalCt);
            var cancellationToken = _connectionCts.Token;

            _state = ConnectionState.Connecting;
            var wsClient = new ClientWebSocket
            {
                Options =
                {
                    KeepAliveInterval = TimeSpan.Zero,
                }
            };
#if NET7_0_OR_GREATER
            wsClient.Options.CollectHttpResponseDetails = true;
#endif
            _wsClient = wsClient;

            if (_headers?.Count > 0)
            {
                foreach (var header in _headers)
                {
                    if (header.Value != null)
                    {
                        wsClient.Options.SetRequestHeader(header.Key, header.Value);
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
#if NET7_0_OR_GREATER
            try
            {
                await wsClient.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException) when (wsClient.HttpStatusCode == HttpStatusCode.Unauthorized)
            {
                throw new NetworkTransportUnauthorizationException("Server rejected the session token (HTTP 401).");
            }
#else
            await wsClient.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
#endif

            if (Opened != null)
            {
                await Opened.Invoke().ConfigureAwait(false);
            }

            var sendChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _sendChannel = sendChannel;

            var generation = Interlocked.Increment(ref _connectionGeneration);

            // No token on Task.Run: a loop that never starts would never run its cleanup.
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(wsClient, generation, cancellationToken));
            _sendLoopTask = Task.Run(() => SendLoopAsync(wsClient, sendChannel, cancellationToken));

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

        private async Task ReceiveLoopAsync(ClientWebSocket wsClient, int generation, CancellationToken cancellationToken)
        {
            byte[]? wsBuffer = null;
            Exception? closeError = null;
            try
            {
                wsBuffer = ArrayPool<byte>.Shared.Rent(WsReceiveBufferSize);
                var messageWriter = new ArrayBufferWriter<byte>(WsReceiveBufferSize);
                while (wsClient.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    messageWriter.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await wsClient.ReceiveAsync(new ArraySegment<byte>(wsBuffer), cancellationToken).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        if (result.Count > 0)
                        {
                            if ((long)messageWriter.WrittenCount + result.Count > MaxWsMessageLen)
                            {
                                throw new InvalidDataException(
                                    $"WebSocket message exceeds receive limit {MaxWsMessageLen}.");
                            }

                            messageWriter.Write(wsBuffer.AsSpan(0, result.Count));
                        }
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        closeError = new SocketClosedException(
                            (int)(result.CloseStatus ?? WebSocketCloseStatus.Empty),
                            string.IsNullOrEmpty(result.CloseStatusDescription) ? null : result.CloseStatusDescription);
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

                    // Don't keep a buffer sized for one exceptionally large message for the whole connection.
                    if (messageWriter.Capacity > MezonTransportFrameCodec.MaxRealtimeFrameLen)
                    {
                        messageWriter = new ArrayBufferWriter<byte>(WsReceiveBufferSize);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                closeError = ex;
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

                if (generation == Volatile.Read(ref _connectionGeneration))
                {
                    _ = Task.Run(() => TeardownAfterReceiveLoopAsync(generation, closeError));
                }
            }
        }

        /// <summary>
        /// Closes the connection after its receive loop ended on its own. A deliberate disconnect or a newer
        /// connection (different generation) makes it a no-op, so an old loop can never close a new connection.
        /// </summary>
        private async Task TeardownAfterReceiveLoopAsync(int generation, Exception? closeError)
        {
            try
            {
                bool closed;
                await _semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (generation != _connectionGeneration || _state != ConnectionState.Connected)
                    {
                        return;
                    }

                    closed = await DisconnectInternalAsync().ConfigureAwait(false);
                    await WaitForBackgroundLoopsAsync().ConfigureAwait(false);
                }
                finally
                {
                    _semaphore.Release();
                }

                if (closed && Closed != null)
                {
                    await Closed.Invoke(closeError).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                if (ErrorOccurred != null)
                {
                    try
                    {
                        await ErrorOccurred.Invoke(ex).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public ValueTask SendAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> data)
        {
            // Read shared fields once: a concurrent disconnect clears them.
            var wsClient = _wsClient;
            var sendChannel = _sendChannel;
            if (wsClient?.State != WebSocketState.Open || _state != ConnectionState.Connected || sendChannel == null)
            {
                return new ValueTask(Task.FromException(new InvalidOperationException(
                    $"Cannot send on WebSocket (wsState={wsClient?.State}, transportState={_state}, sendChannel={sendChannel != null}).")));
            }

            switch (type)
            {
                case MezonMessageType.Api:
                case MezonMessageType.Realtime:
                    return MezonWebSocketFrameCodec.TryQueueRawFrame(sendChannel.Writer, data)
                        ? default
                        : new ValueTask(Task.FromException(new InvalidOperationException("Cannot queue message for sending.")));
                case MezonMessageType.Heartbeat:
                    return new ValueTask(Task.FromException(new InvalidOperationException("WebSocket heartbeat must be sent as a Ping envelope.")));
                default:
                    return new ValueTask(Task.FromException(new InvalidOperationException($"Unsupported WebSocket message type '{type}'.")));
            }
        }

        private async Task SendLoopAsync(ClientWebSocket wsClient, Channel<ReadOnlyMemory<byte>> sendChannel, CancellationToken cancellationToken)
        {
            try
            {
                while (await sendChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (sendChannel.Reader.TryRead(out var msgSend))
                    {
                        try
                        {
                            if (wsClient.State != WebSocketState.Open)
                            {
                                return;
                            }

                            await wsClient.SendAsync(msgSend, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
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
            finally
            {
                // Frames still queued will never be sent; return their pooled buffers. A send racing this sees the
                // completed writer and returns its own buffer.
                sendChannel.Writer.TryComplete();
                while (sendChannel.Reader.TryRead(out var unsent))
                {
                    MezonWebSocketFrameCodec.ReturnPooledSendBuffer(unsent);
                }
            }
        }

        public async Task DisconnectAsync(int closeCode = 1000, string? reason = null)
        {
            bool closed;
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                closed = await DisconnectInternalAsync(closeCode, reason).ConfigureAwait(false);
                await WaitForBackgroundLoopsAsync().ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }

            if (closed && Closed != null)
            {
                await Closed.Invoke(null).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Releases the current connection. Never raises <see cref="Closed"/> (callers do, after releasing the
        /// semaphore) and always ends in <see cref="ConnectionState.Disconnected"/>. Returns false when there was
        /// nothing to close.
        /// </summary>
        private async Task<bool> DisconnectInternalAsync(int closeCode = 1000, string? reason = null)
        {
            if (_state == ConnectionState.Disconnected || _state == ConnectionState.Disconnecting)
            {
                return false;
            }

            _state = ConnectionState.Disconnecting;
            try
            {
                _sendChannel?.Writer.TryComplete();
                TryCancel(_connectionCts);

                var wsClient = _wsClient;
                _wsClient = null;
                if (wsClient != null)
                {
                    try
                    {
                        if (wsClient.State == WebSocketState.Open)
                        {
                            await wsClient.CloseOutputAsync((WebSocketCloseStatus)closeCode, reason ?? "Normal Closure.", CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        wsClient.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                _sendChannel = null;
                _apiChunkBuffers.Clear();
                _state = ConnectionState.Disconnected;
            }

            return true;
        }

        private async Task WaitForBackgroundLoopsAsync()
        {
            var receive = _receiveLoopTask;
            var send = _sendLoopTask;
            _receiveLoopTask = null;
            _sendLoopTask = null;

            try
            {
                if (receive != null)
                {
                    await receive.ConfigureAwait(false);
                }

                if (send != null)
                {
                    await send.ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // Loop faults are reported via ErrorOccurred; cleanup must not throw.
            }

            // The receive loop may have added partial API chunks after the disconnect cleared them.
            _apiChunkBuffers.Clear();
        }

        private static void TryCancel(CancellationTokenSource? cancellationTokenSource)
        {
            try
            {
                cancellationTokenSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
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
            if (_disposed)
            {
                return;
            }

            await DisconnectAsync().ConfigureAwait(false);
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (disposing)
            {
                // The semaphore is intentionally not disposed: a detached teardown may still be waiting on it, and it
                // holds no unmanaged resources unless AvailableWaitHandle is used.
                _sendChannel?.Writer.TryComplete();
                TryCancel(_connectionCts);
                _wsClient?.Dispose();
                _wsClient = null;
                _connectionCts?.Dispose();
                _connectionCts = null;
                _apiChunkBuffers.Clear();
                _state = ConnectionState.Disconnected;
            }
        }
    }
}
