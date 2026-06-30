using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Core.Abstractions;
using Mezon.Net.Transport.Internal;

namespace Mezon.Net.Transport
{
    public class MezonNetworkTcpTransporter : IMezonNetworkTransporter, IDisposable, IAsyncDisposable
    {
        private const string TokenHeaderKey = "token";

        private ConnectionState _state = ConnectionState.Disconnected;
        private TcpClient? _tcpClient;
        private System.IO.Stream? _dataStream;
        private PipeReader? _reader;
        private IDictionary<string, string>? _headers;
        private readonly ConcurrentDictionary<int, ArrayBufferWriter<byte>> _streams = new ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        private CancellationTokenSource? _disconnectCts, _internalCts;
        private CancellationToken _externalCt, _internalCt;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private Channel<ReadOnlyMemory<byte>>? _sendChannel;
        private bool _disposed;
        private int _connectionGeneration;

        public Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
        public Func<Task>? Opened { get; set; }
        public Func<Exception?, Task>? Closed { get; set; }
        public Func<Exception, Task>? ErrorOccurred { get; set; }

        public MezonNetworkTcpTransporter()
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

        public void SetHeader(IDictionary<string, string> headers)
        {
            _headers = headers;
        }

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

                await DisconnectInternalAsync().ConfigureAwait(false);
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
            _tcpClient = new TcpClient
            {
                NoDelay = true,
            };

            await _tcpClient.ConnectAsync(host, port ?? 443).ConfigureAwait(false);

            if (Opened != null)
            {
                await Opened.Invoke().ConfigureAwait(false);
            }

            System.IO.Stream networkStream = _tcpClient.GetStream();
            if (useSsl.HasValue && useSsl.Value)
            {
                var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);
                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    RemoteCertificateValidationCallback = MezonNetworkSettings.DefaultValidateServerCertificate,
                };
                await sslStream.AuthenticateAsClientAsync(sslOptions, _internalCt).ConfigureAwait(false);
                _dataStream = sslStream;
            }
            else
            {
                _dataStream = networkStream;
            }

            await HandshakeAsync(token).ConfigureAwait(false);
            _reader = PipeReader.Create(_dataStream);
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

        private async Task HandshakeAsync(string? token = null)
        {
            byte[]? tokenBytes = null;
            if (token != null)
            {
                tokenBytes = Encoding.UTF8.GetBytes(token);
            }
            else if (_headers != null && _headers.TryGetValue(TokenHeaderKey, out var tokenHeader))
            {
                tokenBytes = Encoding.UTF8.GetBytes(tokenHeader);
            }

            if (tokenBytes == null || tokenBytes.Length == 0)
            {
                if (ErrorOccurred != null)
                {
                    await ErrorOccurred.Invoke(new InvalidOperationException("Unauthorized.")).ConfigureAwait(false);
                }

                throw new InvalidOperationException("Unauthorized.");
            }

            var padding = (4 - (tokenBytes.Length % 4)) & 3;
            var totalLen = tokenBytes.Length + padding;
            var lenDiv4 = totalLen / 4;
            int headerLen = lenDiv4 < 127 ? 2 : 5;
            byte[] handshakeBuffer = ArrayPool<byte>.Shared.Rent(headerLen + totalLen);

            try
            {
                handshakeBuffer[0] = 0xef;
                if (lenDiv4 < 127)
                {
                    handshakeBuffer[1] = (byte)lenDiv4;
                }
                else
                {
                    handshakeBuffer[1] = MezonTransportFrameCodec.AbridgedExtendedPrefix;
                    handshakeBuffer[2] = (byte)lenDiv4;
                    handshakeBuffer[3] = (byte)(lenDiv4 >> 8);
                    handshakeBuffer[4] = (byte)(lenDiv4 >> 16);
                }

                Buffer.BlockCopy(tokenBytes, 0, handshakeBuffer, headerLen, tokenBytes.Length);
                if (padding > 0)
                {
                    Array.Clear(handshakeBuffer, headerLen + tokenBytes.Length, padding);
                }

                await _dataStream!.WriteAsync(handshakeBuffer.AsMemory(0, headerLen + totalLen), _internalCt).ConfigureAwait(false);
                await _dataStream.FlushAsync(_internalCt).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(handshakeBuffer);
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken, int generation)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _dataStream != null)
                {
                    ReadResult result = await _reader!.ReadAsync(cancellationToken).ConfigureAwait(false);
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    if (MessageReceived != null)
                    {
                        while (!buffer.IsEmpty)
                        {
                            var frameStart = buffer.Start;
                            if (!MezonTransportFrameCodec.TryReadFrame(ref buffer, _streams, out var type, out var cid, out var code, out var frame))
                            {
                                if (frameStart.Equals(buffer.Start))
                                {
                                    break;
                                }

                                continue;
                            }

                            var payload = type == MezonMessageType.Abridged
                                ? MezonTransportFrameCodec.TrimPadding(frame)
                                : frame;
                            await MessageReceived.Invoke(type, cid, code, payload).ConfigureAwait(false);
                        }
                    }

                    _reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted)
                    {
                        break;
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
                _reader?.Complete();
                if (generation == _connectionGeneration && _state == ConnectionState.Connected)
                {
                    await DisconnectAsync().ConfigureAwait(false);
                }
            }
        }

        public ValueTask SendAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> data)
        {
            if (_state != ConnectionState.Connected || _tcpClient == null || !_tcpClient.Connected || _sendChannel == null)
            {
                return default;
            }

            switch (type)
            {
                case MezonMessageType.Heartbeat:
                    return MezonTransportFrameCodec.TryQueuePingFrame(_sendChannel.Writer, (ushort)cid)
                        ? default
                        : new ValueTask(Task.FromException(new InvalidOperationException("Cannot queue ping.")));
                case MezonMessageType.Api:
                case MezonMessageType.Abridged:
                    return MezonTransportFrameCodec.TryQueueAbridgedFrame(_sendChannel.Writer, data)
                        ? default
                        : new ValueTask(Task.FromException(new InvalidOperationException("Cannot queue message for sending.")));
                default:
                    return default;
            }
        }

        private async Task SendLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_dataStream == null || _sendChannel == null)
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
                            await _dataStream.WriteAsync(msgSend, cancellationToken).ConfigureAwait(false);
                            await _dataStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            MezonTransportFrameCodec.ReturnPooledSendBuffer(msgSend);
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

        private async Task DisconnectInternalAsync(int closeCode = 1000, bool isDisposing = false)
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

            if (_dataStream != null)
            {
                try
                {
                    await _dataStream.FlushAsync().ConfigureAwait(false);
                }
                finally
                {
                    await _dataStream.DisposeAsync().ConfigureAwait(false);
                    _dataStream = null;
                }
            }

            if (_tcpClient != null)
            {
                try
                {
                    _tcpClient.Close();
                }
                finally
                {
                    _tcpClient.Dispose();
                    _tcpClient = null;
                }
            }

            _streams.Clear();
            _state = ConnectionState.Disconnected;
            if (Closed != null)
            {
                await Closed.Invoke(null).ConfigureAwait(false);
            }
        }

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
                _dataStream?.Dispose();
                _disconnectCts?.Dispose();
                _internalCts?.Dispose();
                _tcpClient?.Dispose();
                _streams.Clear();
            }

            _disposed = true;
        }
    }
}
