using System;
using System.Buffers;

namespace Mezon.Net.Sdk.Agent
{
    /// <summary>
    ///     Incremental SSE decoder. Steady-state parsing uses rented buffers and does not allocate per line.
    ///     The event payload is valid only for the duration of the callback.
    /// </summary>
    internal sealed class AgentSseDecoder : IDisposable
    {
        private byte[] _line = ArrayPool<byte>.Shared.Rent(256);
        private int _lineLength;
        private byte[] _data = ArrayPool<byte>.Shared.Rent(1024);
        private int _dataLength;
        private bool _skipLf;
        private bool _disposed;

        public void Push(ReadOnlySpan<byte> chunk, Action<ReadOnlyMemory<byte>> onEvent)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AgentSseDecoder));
            }

            if (onEvent is null)
            {
                throw new ArgumentNullException(nameof(onEvent));
            }

            for (var i = 0; i < chunk.Length; i++)
            {
                var value = chunk[i];
                if (_skipLf)
                {
                    _skipLf = false;
                    if (value == (byte)'\n')
                    {
                        continue;
                    }
                }

                if (value == (byte)'\r')
                {
                    _skipLf = true;
                    FinishLine(onEvent);
                    continue;
                }

                if (value == (byte)'\n')
                {
                    FinishLine(onEvent);
                    continue;
                }

                AppendLine(value);
            }
        }

        public static bool TryReadEventType(ReadOnlySpan<byte> json, Span<char> destination, out int written)
        {
            written = 0;
            var key = "\"event_type\""u8;
            var start = json.IndexOf(key);
            if (start < 0)
            {
                return false;
            }

            var index = start + key.Length;
            while (index < json.Length && IsSpace(json[index]))
            {
                index++;
            }

            if (index >= json.Length || json[index] != (byte)':')
            {
                return false;
            }

            index++;
            while (index < json.Length && IsSpace(json[index]))
            {
                index++;
            }

            if (index >= json.Length || json[index] != (byte)'"')
            {
                return false;
            }

            index++;
            var valueStart = index;
            while (index < json.Length && json[index] != (byte)'"')
            {
                if (json[index] == (byte)'\\')
                {
                    return false;
                }

                index++;
            }

            if (index >= json.Length)
            {
                return false;
            }

            var value = json.Slice(valueStart, index - valueStart);
            if (value.Length > destination.Length)
            {
                return false;
            }

            written = System.Text.Encoding.UTF8.GetChars(value, destination);
            return written > 0;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ArrayPool<byte>.Shared.Return(_line);
            ArrayPool<byte>.Shared.Return(_data);
        }

        private void FinishLine(Action<ReadOnlyMemory<byte>> onEvent)
        {
            if (_lineLength == 0)
            {
                if (_dataLength == 0)
                {
                    return;
                }

                var length = _dataLength;
                if (length > 0 && _data[length - 1] == (byte)'\n')
                {
                    length--;
                }

                onEvent(new ReadOnlyMemory<byte>(_data, 0, length));
                _dataLength = 0;
                return;
            }

            var line = _line.AsSpan(0, _lineLength);
            _lineLength = 0;
            if (line[0] == (byte)':')
            {
                return;
            }

            var separator = line.IndexOf((byte)':');
            var name = separator < 0 ? line : line.Slice(0, separator);
            if (!name.SequenceEqual("data"u8))
            {
                return;
            }

            var value = separator < 0 ? ReadOnlySpan<byte>.Empty : line.Slice(separator + 1);
            if (value.Length > 0 && value[0] == (byte)' ')
            {
                value = value.Slice(1);
            }

            EnsureData(value.Length + 1);
            value.CopyTo(_data.AsSpan(_dataLength));
            _dataLength += value.Length;
            _data[_dataLength++] = (byte)'\n';
        }

        private void AppendLine(byte value)
        {
            if (_lineLength == _line.Length)
            {
                var grown = ArrayPool<byte>.Shared.Rent(_line.Length * 2);
                _line.AsSpan(0, _lineLength).CopyTo(grown);
                ArrayPool<byte>.Shared.Return(_line);
                _line = grown;
            }

            _line[_lineLength++] = value;
        }

        private void EnsureData(int additional)
        {
            if (_dataLength + additional <= _data.Length)
            {
                return;
            }

            var size = _data.Length;
            while (size < _dataLength + additional)
            {
                size *= 2;
            }

            var grown = ArrayPool<byte>.Shared.Rent(size);
            _data.AsSpan(0, _dataLength).CopyTo(grown);
            ArrayPool<byte>.Shared.Return(_data);
            _data = grown;
        }

        private static bool IsSpace(byte value) => value is (byte)' ' or (byte)'\t';
    }
}
