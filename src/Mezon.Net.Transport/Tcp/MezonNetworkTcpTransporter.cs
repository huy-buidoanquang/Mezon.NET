using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
    /// <remarks>
    /// Lifecycle invariants: <see cref="Closed"/> is never raised while <c>_semaphore</c> is held, so a Closed handler
    /// may call <see cref="DisconnectAsync"/> or <see cref="ConnectAsync"/>. A receive loop that ends on its own hands
    /// teardown to a detached task, so it never waits on the semaphore that a concurrent disconnect holds while it
    /// joins that loop.
    /// </remarks>
    public class MezonNetworkTcpTransporter : IMezonNetworkTransporter, IDisposable, IAsyncDisposable
    {
        private const string TokenHeaderKey = "token";

        /// <summary>Bytes needed to read the status code in "HTTP/1.1 401".</summary>
        private const int HttpStatusLineMinLength = 12;

        private ConnectionState _state = ConnectionState.Disconnected;
        private TcpClient? _tcpClient;
        private System.IO.Stream? _dataStream;
        private IDictionary<string, string>? _headers;
        private readonly ConcurrentDictionary<int, ArrayBufferWriter<byte>> _apiChunkBuffers = new ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        private CancellationTokenSource? _connectionCts;
        private CancellationToken _externalCt = CancellationToken.None;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private Channel<ReadOnlyMemory<byte>>? _sendChannel;
        private Task? _receiveLoopTask;
        private Task? _sendLoopTask;
        private bool _disposed;
        private int _connectionVersion;

        public Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
        public Func<Task>? Opened { get; set; }
        public Func<Exception?, Task>? Closed { get; set; }
        public Func<Exception, Task>? ErrorOccurred { get; set; }

        /// <summary>Token linked into every subsequent connection; cancelling it aborts a connect in progress.</summary>
        public void SetCancelToken(CancellationToken cancellationToken)
        {
            _externalCt = cancellationToken;
        }

        public void SetHeader(IDictionary<string, string> headers)
        {
            _headers = headers;
        }

        public async Task ConnectAsync(string host, int? port = 443, string? token = null, bool? useSsl = false, bool? createStatus = false)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MezonNetworkTcpTransporter));
            }

            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await ConnectInternalAsync(host, port, token, useSsl).ConfigureAwait(false);
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

        private async Task ConnectInternalAsync(string host, int? port, string? token, bool? useSsl)
        {
            // Tear down any previous connection first; joining its loops before that would wait forever.
            await DisconnectInternalAsync().ConfigureAwait(false);
            await WaitForBackgroundLoopsAsync().ConfigureAwait(false);
            _connectionCts?.Dispose();
            _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(_externalCt);
            var cancellationToken = _connectionCts.Token;

            _state = ConnectionState.Connecting;
            _tcpClient = new TcpClient
            {
                NoDelay = true,
            };

            await ConnectSocketAsync(_tcpClient, host, port ?? 443, cancellationToken).ConfigureAwait(false);

            System.IO.Stream networkStream = _tcpClient.GetStream();
            if (useSsl.HasValue && useSsl.Value)
            {
                var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);
                _dataStream = sslStream;
                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                };
                await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _dataStream = networkStream;
            }

            await HandshakeAsync(_dataStream, token, cancellationToken).ConfigureAwait(false);

            if (Opened != null)
            {
                await Opened.Invoke().ConfigureAwait(false);
            }

            var reader = PipeReader.Create(_dataStream);
            var sendChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _sendChannel = sendChannel;
            var dataStream = _dataStream;
            var connectionVer = Interlocked.Increment(ref _connectionVersion);

            // No token on Task.Run: a loop that never starts would never run its cleanup.
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(reader, connectionVer, cancellationToken));
            _sendLoopTask = Task.Run(() => SendLoopAsync(sendChannel, dataStream, cancellationToken));

            _state = ConnectionState.Connected;
        }

        private static async Task ConnectSocketAsync(TcpClient client, string host, int port, CancellationToken cancellationToken)
        {
#if NET5_0_OR_GREATER
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
#else
            using (cancellationToken.Register(static state => ((TcpClient)state!).Dispose(), client))
            {
                try
                {
                    await client.ConnectAsync(host, port).ConfigureAwait(false);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
#endif
        }

        private async Task HandshakeAsync(System.IO.Stream dataStream, string? token, CancellationToken cancellationToken)
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
                    await ErrorOccurred.Invoke(new NetworkTransportUnauthorizationException()).ConfigureAwait(false);
                }

                throw new NetworkTransportUnauthorizationException();
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

                await dataStream.WriteAsync(handshakeBuffer.AsMemory(0, headerLen + totalLen), cancellationToken).ConfigureAwait(false);
                await dataStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(handshakeBuffer);
            }
        }

        private async Task ReceiveLoopAsync(PipeReader reader, int connectionVer, CancellationToken cancellationToken)
        {
            Exception? closeError = null;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    if (!buffer.IsEmpty && LooksLikeHttp(buffer))
                    {
                        if (buffer.Length < HttpStatusLineMinLength && !result.IsCompleted)
                        {
                            reader.AdvanceTo(buffer.Start, buffer.End);
                            continue;
                        }

                        throw CreateHttpRejection(buffer);
                    }

                    while (!buffer.IsEmpty)
                    {
                        var frameStart = buffer.Start;
                        if (!MezonTransportFrameCodec.TryReadFrame(ref buffer, _apiChunkBuffers, out var type, out var cid, out var code, out var frame))
                        {
                            if (frameStart.Equals(buffer.Start))
                            {
                                break;
                            }

                            continue;
                        }

                        if (MessageReceived != null)
                        {
                            await MessageReceived.Invoke(type, cid, code, frame).ConfigureAwait(false);
                        }
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
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
                closeError = ex;
                if (ErrorOccurred != null)
                {
                    await ErrorOccurred.Invoke(ex).ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    await reader.CompleteAsync().ConfigureAwait(false);
                }
                catch
                {
                }

                if (connectionVer == Volatile.Read(ref _connectionVersion))
                {
                    _ = Task.Run(() => TeardownAfterReceiveLoopAsync(connectionVer, closeError));
                }
            }
        }

        /// <summary>
        /// Closes the connection after its receive loop ended on its own (remote close, protocol error). Runs detached
        /// from the loop; a deliberate disconnect or a newer connection makes it a no-op.
        /// </summary>
        private async Task TeardownAfterReceiveLoopAsync(int connectionVer, Exception? closeError)
        {
            try
            {
                bool closed;
                await _semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (connectionVer != _connectionVersion || _state != ConnectionState.Connected)
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

        private static Exception CreateHttpRejection(ReadOnlySequence<byte> buffer)
        {
            // mezon-proto-server answers an invalid handshake token with "HTTP/1.1 401 Unauthorized" and closes.
            Span<byte> statusLine = stackalloc byte[HttpStatusLineMinLength];
            if (buffer.Length >= HttpStatusLineMinLength)
            {
                buffer.Slice(0, HttpStatusLineMinLength).CopyTo(statusLine);
                if (statusLine[9] == (byte)'4' && statusLine[10] == (byte)'0' && statusLine[11] == (byte)'1')
                {
                    return new NetworkTransportUnauthorizationException("Server rejected the session token (HTTP 401).");
                }
            }

            return new InvalidDataException("Server returned HTTP on abridged TCP port (expected binary framing).");
        }

        private static bool LooksLikeHttp(ReadOnlySequence<byte> buffer)
        {
            // Match Rust io_loop: reject HTTP spoken on an abridged TCP socket.
            Span<byte> prefix = stackalloc byte[5];
            if (buffer.Length < 4)
            {
                return false;
            }

            var copyLen = (int)Math.Min(buffer.Length, 5);
            buffer.Slice(0, copyLen).CopyTo(prefix);
            // "HTTP/" or "GET " or "POST "
            if (copyLen >= 5
                && prefix[0] == (byte)'H'
                && prefix[1] == (byte)'T'
                && prefix[2] == (byte)'T'
                && prefix[3] == (byte)'P'
                && prefix[4] == (byte)'/')
            {
                return true;
            }

            if (copyLen >= 4
                && prefix[0] == (byte)'G'
                && prefix[1] == (byte)'E'
                && prefix[2] == (byte)'T'
                && prefix[3] == (byte)' ')
            {
                return true;
            }

            return copyLen >= 5
                && prefix[0] == (byte)'P'
                && prefix[1] == (byte)'O'
                && prefix[2] == (byte)'S'
                && prefix[3] == (byte)'T'
                && prefix[4] == (byte)' ';
        }

        public ValueTask SendAsync(MezonMessageType type, int cid, ReadOnlyMemory<byte> data)
        {
            // Read shared fields once: a concurrent disconnect clears them.
            var tcpClient = _tcpClient;
            var sendChannel = _sendChannel;
            if (_state != ConnectionState.Connected || tcpClient == null || !tcpClient.Connected || sendChannel == null)
            {
                return new ValueTask(Task.FromException(new InvalidOperationException(
                    $"Cannot send on TCP (transportState={_state}, tcpConnected={tcpClient?.Connected}, sendChannel={sendChannel != null}).")));
            }

            switch (type)
            {
                case MezonMessageType.Heartbeat:
                    return MezonTransportFrameCodec.TryQueuePingFrame(sendChannel.Writer, (ushort)cid)
                        ? default
                        : new ValueTask(Task.FromException(new InvalidOperationException("Cannot queue ping.")));
                case MezonMessageType.Api:
                case MezonMessageType.Realtime:
                    try
                    {
                        return MezonTransportFrameCodec.TryQueueRealtimeFrame(sendChannel.Writer, data)
                            ? default
                            : new ValueTask(Task.FromException(new InvalidOperationException("Cannot queue message for sending.")));
                    }
                    catch (NetworkTransportPayloadTooLargeException ex)
                    {
                        // Oversized send fails this request only; connection and receive loop stay open.
                        return new ValueTask(Task.FromException(ex));
                    }
                default:
                    return new ValueTask(Task.FromException(new InvalidOperationException($"Unsupported TCP message type '{type}'.")));
            }
        }

        private async Task SendLoopAsync(Channel<ReadOnlyMemory<byte>> sendChannel, System.IO.Stream dataStream, CancellationToken cancellationToken)
        {
            try
            {
                while (await sendChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (sendChannel.Reader.TryRead(out var msgSend))
                    {
                        try
                        {
                            await dataStream.WriteAsync(msgSend, cancellationToken).ConfigureAwait(false);
                            await dataStream.FlushAsync(cancellationToken).ConfigureAwait(false);
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
            finally
            {
                // Frames still queued will never be sent; return their pooled buffers. A send racing this sees the
                // completed writer and returns its own buffer.
                sendChannel.Writer.TryComplete();
                while (sendChannel.Reader.TryRead(out var unsent))
                {
                    MezonTransportFrameCodec.ReturnPooledSendBuffer(unsent);
                }
            }
        }

        public async Task DisconnectAsync(int closeCode = 1000, string? reason = null)
        {
            bool closed;
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                closed = await DisconnectInternalAsync().ConfigureAwait(false);
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
        private async Task<bool> DisconnectInternalAsync()
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

                var dataStream = _dataStream;
                _dataStream = null;
                if (dataStream != null)
                {
                    try
                    {
                        await dataStream.FlushAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                    try
                    {
                        await dataStream.DisposeAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }

                var tcpClient = _tcpClient;
                _tcpClient = null;
                if (tcpClient != null)
                {
                    try
                    {
                        tcpClient.Close();
                    }
                    catch
                    {
                    }

                    tcpClient.Dispose();
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
                _dataStream?.Dispose();
                _dataStream = null;
                _tcpClient?.Dispose();
                _tcpClient = null;
                _connectionCts?.Dispose();
                _connectionCts = null;
                _apiChunkBuffers.Clear();
                _state = ConnectionState.Disconnected;
            }
        }
    }
}
