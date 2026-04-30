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

namespace Mezon.Net.Transport.Tcp
{
    public class MezonNetworkTcpTransporter : IMezonNetworkTransporter, IDisposable, IAsyncDisposable
    {
        private const string TokenHeaderKey = "token";
        private const byte PongPrefix = 0x00;
        private const byte ApiPrefix = 0xff;
        private const byte AbridgedExtendedPrefix = 0x7f;
        private const int FinishCode = 0xff;
        private const int CodeLength = 3;
        private const int RawHeaderLength = 7;
        private const int PayloadHeaderLength = 11;

        private ConnectionState _state = ConnectionState.Disconnected;
        private TcpClient? _tcpClient;
        private Stream? _dataStream;
        private PipeReader? _reader;
        private IDictionary<string, string>? _headers;
        private readonly ConcurrentDictionary<int, ArrayBufferWriter<byte>> _streams = new ConcurrentDictionary<int, ArrayBufferWriter<byte>>();
        private CancellationTokenSource? _disconnectCts, _internalCts;
        private CancellationToken _externalCt, _internalCt;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private Channel<ReadOnlyMemory<byte>>? _sendChannel;
        private bool _disposed;

        public Func<ReadOnlyMemory<byte>, ValueTask>? MessageReceived { get; set; }
        public Func<Task>? Opened { get; set; }
        public Func<Task>? Ready { get; set; }
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

            Stream networkStream = _tcpClient.GetStream();
            if (useSsl.HasValue && useSsl.Value)
            {
                var sslStream = new SslStream(
                    networkStream,
                    leaveInnerStreamOpen: false,
                    userCertificateValidationCallback: (s, cert, chain, err) =>
                    {
                        // For simplicity, we accept all certificates. In production, you should validate the server certificate properly.
                        return true;
                    });
                await sslStream.AuthenticateAsClientAsync(host).ConfigureAwait(false);
                _dataStream = sslStream;
            }
            else
            {
                _dataStream = networkStream;
            }

            await HandshakeAsync(host, token).ConfigureAwait(false);
            _reader = PipeReader.Create(_dataStream);
            _sendChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _ = Task.Run(() => ReceiveLoopAsync(_internalCt), _internalCt);
            _ = Task.Run(() => SendLoopAsync(_internalCt), _internalCt);

            _state = ConnectionState.Connected;

            if (Ready != null)
            {
                await Ready.Invoke().ConfigureAwait(false);
            }
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

            var padding = (4 - (tokenBytes.Length % 4)) % 4;
            var totalLen = tokenBytes.Length + padding;

            // Handshake buffer: magic byte (0xef) + header length (1 byte) + token bytes + padding
            byte[] handshakeBuffer = ArrayPool<byte>.Shared.Rent(totalLen + 2);
            try
            {
                handshakeBuffer[0] = 0xef;
                handshakeBuffer[1] = (byte)(totalLen / 4);
                Buffer.BlockCopy(tokenBytes, 0, handshakeBuffer, 2, tokenBytes.Length);

                await _dataStream!.WriteAsync(handshakeBuffer.AsMemory(0, totalLen + 2), _internalCt).ConfigureAwait(false);
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
                    ReadResult result = await _reader!.ReadAsync(cancellationToken).ConfigureAwait(false);
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    while (TryReadFrame(ref buffer, out var frame))
                    {
                        Console.WriteLine($"Received frame of length {frame.ToString()}");
                        if (MessageReceived != null)
                        {
                            await MessageReceived.Invoke(frame).ConfigureAwait(false);
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

        private bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> frame)
        {
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
                    if (TryPongFrame(ref reader, prefix, out frame))
                    {
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }
                    return false;
                // Raw API data frame: prefix(1 byte) + CID(2 bytes) + Code(4 bytes) + PayloadLen(4 bytes) + Payload
                case ApiPrefix:
                    if (TryReadApiFrame(ref reader, prefix, out frame))
                    {
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }
                    return false;
                default:
                    if (TryReadAbridgedFrame(ref reader, prefix, out frame))
                    {
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }
                    return false;
            }
        }

        private bool TryPongFrame(ref SequenceReader<byte> reader, byte prefix, out ReadOnlyMemory<byte> frame)
        {
            frame = default;
            if (prefix != PongPrefix)
            {
                return false;
            }

            // Pong frame: prefix(1 byte) + CID(2 bytes)
            if (reader.Remaining < 2)
            {
                return false;
            }

            if (!reader.TryRead(out byte cidP1) || !reader.TryRead(out byte cidP2))
            {
                return false;
            }
            frame = new byte[3] { 0x00, cidP1, cidP2 };
            return true;
        }

        private bool TryReadApiFrame(ref SequenceReader<byte> reader, byte prefix, out ReadOnlyMemory<byte> frame)
        {
            frame = default;
            if (prefix != ApiPrefix)
            {
                return false;
            }

            // Raw data frame: prefix(1 byte) + CID(2 bytes) + Code(4 bytes) + PayloadLen(4 bytes) + Payload
            if (reader.Remaining < 10)
            {
                return false;
            }

            reader.TryReadBigEndian(out short cidShort);
            int cid = (ushort)cidShort;
            reader.TryReadBigEndian(out int code);
            reader.TryReadBigEndian(out int payloadLen);
            if (reader.Remaining < payloadLen)
            {
                return false;
            }

            var payloadSequence = reader.Sequence.Slice(reader.Position, payloadLen);
            ProcessRawApiData(cid, code, payloadSequence, out frame);
            reader.Advance(payloadLen);
            return true;
        }

        private void ProcessRawApiData(int cid, int code, ReadOnlySequence<byte> payload, out ReadOnlyMemory<byte> frame)
        {
            frame = default;
            var writer = _streams.GetOrAdd(cid, _ => new ArrayBufferWriter<byte>(initialCapacity: 4096));
            int payloadLen = (int)payload.Length;
            var span = writer.GetSpan(payloadLen);
            payload.CopyTo(span);
            writer.Advance(payloadLen);

            if ((code & 0xffff) == FinishCode)
            {
                var completeData = writer.WrittenMemory;
                int responseCode = (code >> 16) & 0xffff;
                byte[] finalFrame = ArrayPool<byte>.Shared.Rent(11 + completeData.Length);
                try
                {
                    finalFrame[0] = ApiPrefix;
                    finalFrame[1] = (byte)(cid >> 8);
                    finalFrame[2] = (byte)(cid & 0xff);

                    // Code (4 bytes)
                    finalFrame[3] = (byte)(responseCode >> 24);
                    finalFrame[4] = (byte)(responseCode >> 16);
                    finalFrame[5] = (byte)(responseCode >> 8);
                    finalFrame[6] = (byte)(responseCode & 0xff);

                    // Payload length (4 bytes)
                    finalFrame[7] = (byte)(completeData.Length >> 24);
                    finalFrame[8] = (byte)(completeData.Length >> 16);
                    finalFrame[9] = (byte)(completeData.Length >> 8);
                    finalFrame[10] = (byte)(completeData.Length & 0xff);

                    completeData.CopyTo(finalFrame.AsMemory(11));
                    frame = finalFrame.AsMemory(0, 11 + completeData.Length).ToArray();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(finalFrame);
                    _streams.TryRemove(cid, out _);
                }
            }
        }

        private bool TryReadAbridgedFrame(ref SequenceReader<byte> reader, byte prefix, out ReadOnlyMemory<byte> frame)
        {
            frame = default;
            int payloadLen = 0;

            // TODO: Determine the exact length of the payload by specifying the quantity of 4-byte blocks.
            if (prefix < AbridgedExtendedPrefix)
            {
                // Case 1: prefix is quantity of 4-byte blocks
                // payloadLength = prefix * 4
                payloadLen = prefix * 4;
            }
            else if (prefix == AbridgedExtendedPrefix)
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

            var payloadSequence = reader.Sequence.Slice(reader.Position, payloadLen);
            frame = payloadSequence.ToArray();
            reader.Advance(payloadLen);
            return true;
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> data)
        {
            if (_state != ConnectionState.Connected || _tcpClient == null || !_tcpClient.Connected || _sendChannel == null)
            {
#if NETSTANDARD2_1
                return new ValueTask();
#else
                return ValueTask.CompletedTask;
#endif
            }

            if (!_sendChannel.Writer.TryWrite(data))
            {
                return new ValueTask(Task.FromException(new Exception("Cannot queue message for sending.")));
            }

#if NETSTANDARD2_1
            return new ValueTask();
#else
                return ValueTask.CompletedTask;
#endif
        }

        private async Task SendLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var data in _sendChannel!.Reader.ReadAllAsync(cancellationToken))
                {
                    if (_dataStream == null)
                    {
                        if (ErrorOccurred != null)
                        {
                            await ErrorOccurred.Invoke(new Exception("Connection is not established.")).ConfigureAwait(false);
                        }
                        break;
                    }

                    await _dataStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                    await _dataStream.FlushAsync(cancellationToken).ConfigureAwait(false);
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
