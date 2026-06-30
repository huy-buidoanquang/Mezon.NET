using System.Buffers.Binary;
using System.Text;

namespace Mezon.Net.Transport.Tests.Helpers;

internal static class MezonTransportFrameBuilder
{
    public const byte ApiPrefix = 0xff;
    public const byte PongPrefix = 0x00;
    public const byte HandshakePrefix = 0xef;
    public const byte AbridgedExtendedPrefix = 0x7f;
    public const int FinishFlag = 0xff;

    public static byte[] BuildHandshake(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var padding = (4 - (tokenBytes.Length % 4)) & 3;
        var totalLen = tokenBytes.Length + padding;
        var lenDiv4 = totalLen / 4;
        var headerLen = lenDiv4 < 127 ? 2 : 5;
        var buffer = new byte[headerLen + totalLen];
        buffer[0] = HandshakePrefix;
        if (lenDiv4 < 127)
        {
            buffer[1] = (byte)lenDiv4;
        }
        else
        {
            buffer[1] = AbridgedExtendedPrefix;
            buffer[2] = (byte)lenDiv4;
            buffer[3] = (byte)(lenDiv4 >> 8);
            buffer[4] = (byte)(lenDiv4 >> 16);
        }

        tokenBytes.CopyTo(buffer.AsSpan(headerLen));
        return buffer;
    }

    public static byte[] BuildPongFrame(int cid)
    {
        var buffer = new byte[3];
        buffer[0] = PongPrefix;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(1), (ushort)cid);
        return buffer;
    }

    public static byte[] BuildApiFrame(int cid, int responseCode, bool finish, ReadOnlySpan<byte> payload)
    {
        var buffer = new byte[11 + payload.Length];
        buffer[0] = ApiPrefix;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(1), (ushort)cid);
        var codeField = (responseCode << 16) | (finish ? FinishFlag : 0);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(3), codeField);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(7), payload.Length);
        payload.CopyTo(buffer.AsSpan(11));
        return buffer;
    }

    public static byte[] BuildAbridgedFrame(ReadOnlySpan<byte> payload, bool padToFour = true)
    {
        var data = payload;
        var padding = padToFour ? (4 - (data.Length % 4)) & 3 : 0;
        var payloadWithPadding = data.Length + padding;
        var lenDiv4 = payloadWithPadding / 4;
        var headerSize = lenDiv4 < 127 ? 1 : 4;
        var buffer = new byte[headerSize + payloadWithPadding];
        if (headerSize == 1)
        {
            buffer[0] = (byte)lenDiv4;
        }
        else
        {
            buffer[0] = AbridgedExtendedPrefix;
            buffer[1] = (byte)lenDiv4;
            buffer[2] = (byte)(lenDiv4 >> 8);
            buffer[3] = (byte)(lenDiv4 >> 16);
        }

        data.CopyTo(buffer.AsSpan(headerSize));
        return buffer;
    }

    public static async Task ReadHandshakeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var header = new byte[2];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        if (header[0] != HandshakePrefix)
        {
            throw new InvalidOperationException($"Expected handshake prefix 0x{HandshakePrefix:x2}, got 0x{header[0]:x2}.");
        }

        int totalLen;
        if (header[1] < 127)
        {
            totalLen = header[1] * 4;
        }
        else
        {
            var ext = new byte[3];
            await stream.ReadExactlyAsync(ext, cancellationToken).ConfigureAwait(false);
            totalLen = (ext[0] | (ext[1] << 8) | (ext[2] << 16)) * 4;
        }

        var tokenBuffer = new byte[totalLen];
        await stream.ReadExactlyAsync(tokenBuffer, cancellationToken).ConfigureAwait(false);
    }
}
