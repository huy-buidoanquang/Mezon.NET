using System;
using System.Buffers;
using System.Buffers.Binary;
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

namespace Mezon.Net.Transport
{
    public class MezonNetworkTcpTransporter : IMezonNetworkTransporter, IDisposable, IAsyncDisposable
    {
        private const string TokenHeaderKey = "token";
        private const byte PongPrefix = 0x00;
        private const byte ApiPrefix = 0xff;
        private const byte AbridgedTpcExtendedPrefix = 0x7f;
        private const int FinishFlag = 0xff;

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

        public Func<MezonMessageType, int, int, ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
        public Func<Task>? Opened { get; set; }
        public Func<Exception?, Task>? Closed { get; set; }
        public Func<Exception, Task>? ErrorOccurred { get; set; }

        public MezonNetworkTcpTransporter()
        {
            _state = ConnectionState.Disconnected;
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
                await DisconnectAsync().ConfigureAwait(false);
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
            _tcpClient = new TcpClient()
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
                    RemoteCertificateValidationCallback = (s, cert, chain, err) =>
                    {
                        // For simplicity, we accept all certificates. In production, you should validate the server certificate properly.
                        return true;
                    },
                };
                await sslStream.AuthenticateAsClientAsync(sslOptions, _internalCt).ConfigureAwait(false);
                _dataStream = sslStream;
            }
            else
            {
                _dataStream = networkStream;
            }
            await HandshakeAsync(host, token).ConfigureAwait(false);
            _reader = PipeReader.Create(_dataStream);
            _sendChannel = System.Threading.Channels.Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _ = Task.Run(() => ReceiveLoopAsync(_internalCt), _internalCt);
            _ = Task.Run(() => SendLoopAsync(_internalCt), _internalCt);

            _state = ConnectionState.Connected;
        }

        private async Task HandshakeAsync(string host, string? token = null)
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
                    await ErrorOccurred.Invoke(new Exception("Unauthorized.")).ConfigureAwait(false);
                }
                return;
            }

            var padding = (4 - (tokenBytes.Length % 4)) & 3;
            var totalLen = tokenBytes.Length + padding;
            var lenDiv4 = totalLen / 4;

            int headerLen = (lenDiv4 < 127) ? 2 : 5;
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
                    handshakeBuffer[1] = 0x7f;
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

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _dataStream != null)
                {
                    if (MessageReceived != null)
                    {
                        ReadResult result = await _reader!.ReadAsync(cancellationToken).ConfigureAwait(false);
                        ReadOnlySequence<byte> buffer = result.Buffer;
                        while (TryReadFrame(ref buffer, out var type, out var cid, out var code, out var frame))
                        {
                            await MessageReceived.Invoke(type, cid, code, type == MezonMessageType.Abridged ? TrimPadding(frame) : frame).ConfigureAwait(false);
                        }
                        _reader.AdvanceTo(buffer.Start, buffer.End);
                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
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
                await DisconnectAsync().ConfigureAwait(false);
            }
        }

        private bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out MezonMessageType type, out int cid, out int code, out ReadOnlyMemory<byte> frame)
        {
            type = default;
            cid = default;
            code = default;
            frame = default;
            if (buffer.IsEmpty)
            {
                return false;
            }

            var reader = new SequenceReader<byte>(buffer);
            if (!reader.TryRead(out byte prefix))
            {
                return false;
            }

            // TODO: Verify prefix to determine frame type and length
            // prefix 0x00: Pong (3 bytes total)
            // prefix 0xff: Api data (11 bytes header)
            // prefix >= 0x7f: Abridged frame (11 bytes header)
            switch (prefix)
            {
                // Pong frame: prefix(1 byte) + CID(2 bytes)
                case PongPrefix:
                    if (TryReadPongFrame(ref reader, prefix, out cid, out frame))
                    {
                        type = MezonMessageType.Heartbeat;
                        code = 0;
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }
                    return false;
                // Raw API data frame: prefix(1 byte) + CID(2 bytes) + Code(4 bytes) + PayloadLen(4 bytes) + Payload
                case ApiPrefix:
                    if (TryReadApiFrame(ref reader, prefix, out cid, out code, out frame))
                    {
                        type = MezonMessageType.Api;
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }
                    return false;
                default:
                    if (TryReadAbridgedFrame(ref reader, prefix, out cid, out frame))
                    {
                        type = MezonMessageType.Abridged;
                        code = 0;
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }
                    return false;
            }
        }

        private bool TryReadPongFrame(ref SequenceReader<byte> reader, byte prefix, out int cid, out ReadOnlyMemory<byte> frame)
        {
            frame = default;
            cid = default;
            if (prefix != PongPrefix)
            {
                return false;
            }

            // Pong frame: prefix(1 byte) + CID(2 bytes)
            if (reader.Remaining < 2)
            {
                return false;
            }

            if (!reader.TryReadBigEndian(out short cidS))
            {
                return false;
            }
            cid = (int)cidS;
            return true;
        }

        private bool TryReadApiFrame(ref SequenceReader<byte> reader, byte prefix, out int cid, out int code, out ReadOnlyMemory<byte> frame)
        {
            frame = default;
            cid = default;
            code = default;
            if (prefix != ApiPrefix)
            {
                return false;
            }

            // Raw data frame: prefix(1 byte) + CID(2 bytes) + Code(4 bytes) + PayloadLen(4 bytes) + Payload
            if (reader.Remaining < 10)
            {
                return false;
            }

            reader.TryReadBigEndian(out short cidFrame);
            cid = cidFrame;

            // Code is structured as: [ResponseCode (2 bytes)][FinishFlag (2 bytes)]
            reader.TryReadBigEndian(out int codeFrame);
            reader.TryReadBigEndian(out int payloadLen);
            if (reader.Remaining < payloadLen)
            {
                return false;
            }

            var writer = _streams.GetOrAdd(cid, _ => new ArrayBufferWriter<byte>(initialCapacity: 4096));
            var span = writer.GetSpan(payloadLen);
            reader.TryCopyTo(span);
            writer.Advance(payloadLen);
            code = (codeFrame >> 16) & 0xffff;
            var finishFlag = codeFrame & 0xffff;
            if (finishFlag == FinishFlag)
            {
                frame = writer.WrittenMemory;
                _streams.TryRemove(cid, out _);
            }
            reader.Advance(payloadLen);
            return true;
        }

        private bool TryReadAbridgedFrame(ref SequenceReader<byte> reader, byte prefix, out int cid, out ReadOnlyMemory<byte> frame)
        {
            frame = default;
            cid = default;
            int payloadLen = 0;
            // TODO: Determine the exact length of the payload by specifying the quantity of 4-byte blocks.
            if (prefix < AbridgedTpcExtendedPrefix)
            {
                // Case 1: prefix is quantity of 4-byte blocks
                // payloadLength = prefix * 4
                payloadLen = prefix * 4;
            }
            else if (prefix == AbridgedTpcExtendedPrefix)
            {
                // Case 2: Prefix is 0x7f, the quantity of 4-byte blocks is specified in the next 3 bytes (Little Endian)
                if (reader.Remaining < 3)
                {
                    return false;
                }

                reader.TryRead(out byte l1);
                reader.TryRead(out byte l2);
                reader.TryRead(out byte l3);

                payloadLen = (l1 | (l2 << 8) | (l3 << 16)) * 4;
            }
            else
            {
                return false;
            }

            if (reader.Remaining < payloadLen)
            {
                return false;
            }

            var rawPayloadSequence = reader.Sequence.Slice(reader.Position, payloadLen);
            if (rawPayloadSequence.IsSingleSegment)
            {
                frame = rawPayloadSequence.First.Slice(0, payloadLen);
            }
            else
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(payloadLen);
                rawPayloadSequence.CopyTo(buffer);
                frame = new ReadOnlyMemory<byte>(buffer, 0, payloadLen);
            }

            reader.Advance(payloadLen);
            return true;
        }

        private static ReadOnlyMemory<byte> TrimPadding(ReadOnlyMemory<byte> frame)
        {
            var span = frame.Span;
            int len = span.Length;
            int maxPadding = len < 3 ? len : 3;
            int trimmed = 0;
            while (trimmed < maxPadding && span[len - 1 - trimmed] == 0x00)
            {
                trimmed++;
            }

            return trimmed == 0 ? frame : frame.Slice(0, len - trimmed);
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
                    return SendPingAsync((ushort)cid);
                case MezonMessageType.Api:
                    return SendDataAsync(data);
                case MezonMessageType.Abridged:
                    return SendDataAsync(data);
                default:
                    break;
            }

            return default;
        }

        private ValueTask SendDataAsync(ReadOnlyMemory<byte> data)
        {
            int padding = (4 - (data.Length % 4)) & 3;
            int payloadWithPadding = data.Length + padding;
            int lenDiv4 = payloadWithPadding / 4;
            int headerSize = lenDiv4 < 127 ? 1 : 4;
            int totalSize = headerSize + payloadWithPadding;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(totalSize);
            try
            {
                Span<byte> span = buffer.AsSpan(0, totalSize);
                if (headerSize == 1)
                {
                    span[0] = (byte)lenDiv4;
                }
                else
                {
                    span[0] = AbridgedTpcExtendedPrefix;
                    span[1] = (byte)lenDiv4;
                    span[2] = (byte)(lenDiv4 >> 8);
                    span[3] = (byte)(lenDiv4 >> 16);
                }
                data.Span.CopyTo(span.Slice(headerSize));
                if (padding > 0)
                {
                    span.Slice(headerSize + data.Length, padding).Clear();
                }
                if (!_sendChannel!.Writer.TryWrite(buffer.AsMemory(0, totalSize)))
                {
                    return new ValueTask(Task.FromException(new Exception("Cannot queue message for sending.")));
                }

                return default;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private ValueTask SendPingAsync(ushort cid)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(3);
            try
            {
                Span<byte> span = buffer.AsSpan(0, 3);
                span[0] = 0x00;
                BinaryPrimitives.WriteUInt16BigEndian(span.Slice(1), cid);

                if (!_sendChannel!.Writer.TryWrite(buffer.AsMemory(0, 3)))
                {
                    return new ValueTask(Task.FromException(new Exception("Cannot queue ping.")));
                }

                return default;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
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
                        await ErrorOccurred.Invoke(new Exception("Connection is not established.")).ConfigureAwait(false);
                    }
                }

                while (await _sendChannel!.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (_sendChannel!.Reader.TryRead(out var msgSend))
                    {
                        try
                        {
                            await _dataStream!.WriteAsync(msgSend, cancellationToken).ConfigureAwait(false);
                            await _dataStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {

                            throw;
                        }
                        finally
                        {
                            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(msgSend, out var segment) && segment.Array != null && segment.Count > 0)
                            {
                                ArrayPool<byte>.Shared.Return(segment.Array);
                            }
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
            await _semaphore.WaitAsync();
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
                catch { }
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
                    await _dataStream.DisposeAsync();
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
            if (!_disposed)
            {
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
}
