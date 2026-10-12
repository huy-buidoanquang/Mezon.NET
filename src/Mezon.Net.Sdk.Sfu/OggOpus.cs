using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mezon.Net.Sdk.Sfu;

/// <summary>Opus packet helpers (RFC 6716).</summary>
public static class OpusPacket
{
    /// <summary>The audio duration of an Opus packet, from its TOC byte (RFC 6716 §3.1).</summary>
    public static TimeSpan Duration(ReadOnlySpan<byte> packet)
    {
        if (packet.Length == 0)
        {
            throw new InvalidDataException("oggopus: empty packet");
        }

        var toc = packet[0];
        var config = toc >> 3;
        long frameMicros = config switch
        {
            <= 11 => new long[] { 10_000, 20_000, 40_000, 60_000 }[config % 4],
            <= 15 => new long[] { 10_000, 20_000 }[config % 2],
            _ => new long[] { 2_500, 5_000, 10_000, 20_000 }[config % 4],
        };
        int frames;
        switch (toc & 3)
        {
            case 0:
                frames = 1;
                break;
            case 1:
            case 2:
                frames = 2;
                break;
            default:
                if (packet.Length < 2)
                {
                    throw new InvalidDataException("oggopus: truncated code 3 packet");
                }

                frames = packet[1] & 0x3F;
                break;
        }

        return TimeSpan.FromTicks(frameMicros * frames * 10);
    }
}

/// <summary>The OpusHead header of an Ogg Opus stream.</summary>
public readonly record struct OggOpusHead(byte Channels, ushort PreSkip, uint SampleRate);

/// <summary>
/// Reads the Opus packets of an Ogg Opus stream (RFC 7845) packet by packet, reassembling packets that span pages,
/// for bots that play Opus without decoding it.
/// </summary>
public sealed class OggOpusReader
{
    private readonly Stream _stream;
    private readonly Queue<byte[]> _pending = new();
    private readonly List<byte> _partial = new();

    /// <summary>Reads the OpusHead and OpusTags headers.</summary>
    /// <exception cref="InvalidDataException">The stream is not Ogg Opus.</exception>
    public OggOpusReader(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        var head = NextPacket() ?? throw Invalid("missing OpusHead");
        if (head.Length < 19 || Encoding.ASCII.GetString(head, 0, 8) != "OpusHead")
        {
            throw Invalid("not an Ogg Opus stream");
        }

        Head = new OggOpusHead(head[9], (ushort)(head[10] | head[11] << 8), (uint)(head[12] | head[13] << 8 | head[14] << 16 | head[15] << 24));
        var tags = NextPacket() ?? throw Invalid("missing OpusTags");
        if (tags.Length < 8 || Encoding.ASCII.GetString(tags, 0, 8) != "OpusTags")
        {
            throw Invalid("not an Ogg Opus stream");
        }
    }

    public OggOpusHead Head { get; }

    private static InvalidDataException Invalid(string message) => new($"oggopus: {message}");

    /// <summary>The next Opus packet and its duration; null at the end of the stream.</summary>
    public (byte[] Packet, TimeSpan Duration)? ReadPacket()
    {
        var packet = NextPacket();
        return packet is null ? null : (packet, OpusPacket.Duration(packet));
    }

    private byte[]? NextPacket()
    {
        while (_pending.Count == 0)
        {
            if (!ReadPage())
            {
                return null;
            }
        }

        return _pending.Dequeue();
    }

    private void ReadExactly(byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            var read = _stream.Read(buffer, offset, count);
            if (read == 0)
            {
                throw new EndOfStreamException("oggopus: truncated page");
            }

            offset += read;
            count -= read;
        }
    }

    private bool ReadPage()
    {
        var header = new byte[27];
        var first = _stream.Read(header, 0, 1);
        if (first == 0)
        {
            return false;
        }

        ReadExactly(header, 1, 26);
        if (header[0] != (byte)'O' || header[1] != (byte)'g' || header[2] != (byte)'g' || header[3] != (byte)'S' || header[4] != 0)
        {
            throw Invalid("bad page header");
        }

        var segments = new byte[header[26]];
        ReadExactly(segments, 0, segments.Length);
        var size = 0;
        foreach (var s in segments)
        {
            size += s;
        }

        var body = new byte[size];
        ReadExactly(body, 0, size);
        var offset = 0;
        foreach (var s in segments)
        {
            for (var i = 0; i < s; i++)
            {
                _partial.Add(body[offset + i]);
            }

            offset += s;
            if (s < 255)
            {
                if (_partial.Count > 0)
                {
                    _pending.Enqueue(_partial.ToArray());
                }

                _partial.Clear();
            }
        }

        return true;
    }
}

/// <summary>Writes an Ogg Opus stream (RFC 7845), one packet per page, for recorders that store Opus without decoding it.</summary>
public sealed class OggOpusWriter : IDisposable
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    private readonly Stream _stream;
    private readonly uint _serial;
    private uint _sequence;
    private ulong _granule;
    private bool _finished;

    /// <summary>Writes the OpusHead (48 kHz input, no pre-skip) and OpusTags headers.</summary>
    public OggOpusWriter(Stream stream, byte channels = 2)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _serial = (uint)Environment.TickCount ^ (uint)DateTime.UtcNow.Ticks;
        var head = new List<byte>(Encoding.ASCII.GetBytes("OpusHead")) { 1, channels, 0, 0 };
        head.AddRange(Le32(48_000u));
        head.AddRange(new byte[] { 0, 0, 0 });
        Page(head.ToArray(), 0, 2);
        var vendor = Encoding.ASCII.GetBytes("Mezon.Net.Sdk.Sfu");
        var tags = new List<byte>(Encoding.ASCII.GetBytes("OpusTags"));
        tags.AddRange(Le32((uint)vendor.Length));
        tags.AddRange(vendor);
        tags.AddRange(Le32(0u));
        Page(tags.ToArray(), 0, 0);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var r = i << 24;
            for (var bit = 0; bit < 8; bit++)
            {
                r = (r & 0x8000_0000) != 0 ? (r << 1) ^ 0x04C1_1DB7 : r << 1;
            }

            table[i] = r;
        }

        return table;
    }

    private static byte[] Le32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] Le64(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    /// <summary>The Ogg checksum: CRC-32 with polynomial 0x04C11DB7, no reflection, zero init.</summary>
    internal static uint OggCrc(ReadOnlySpan<byte> data)
    {
        uint crc = 0;
        foreach (var b in data)
        {
            crc = (crc << 8) ^ CrcTable[(byte)(crc >> 24) ^ b];
        }

        return crc;
    }

    /// <summary>Appends one Opus packet; its duration (from the TOC) advances the granule position.</summary>
    public void WritePacket(ReadOnlySpan<byte> packet)
    {
        if (_finished)
        {
            throw new InvalidOperationException("oggopus: the stream is finished");
        }

        var duration = OpusPacket.Duration(packet);
        _granule += (ulong)(duration.Ticks * 48_000 / TimeSpan.TicksPerSecond);
        Page(packet.ToArray(), _granule, 0);
    }

    /// <summary>Writes an end-of-stream page without packets and flushes. Idempotent.</summary>
    public void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Page(null, _granule, 4);
        _stream.Flush();
    }

    /// <summary>Finishes the stream (the underlying stream is not disposed).</summary>
    public void Dispose() => Finish();

    private void Page(byte[]? packet, ulong granule, byte flags)
    {
        var segments = new List<byte>();
        if (packet is not null)
        {
            var left = packet.Length;
            while (left >= 255)
            {
                segments.Add(255);
                left -= 255;
            }

            segments.Add((byte)left);
        }

        if (segments.Count > 255)
        {
            throw new InvalidDataException("oggopus: packet too large for one page");
        }

        var page = new List<byte>(27 + segments.Count + (packet?.Length ?? 0));
        page.AddRange(Encoding.ASCII.GetBytes("OggS"));
        page.Add(0);
        page.Add(flags);
        page.AddRange(Le64(granule));
        page.AddRange(Le32(_serial));
        page.AddRange(Le32(_sequence));
        page.AddRange(new byte[4]);
        page.Add((byte)segments.Count);
        page.AddRange(segments);
        if (packet is not null)
        {
            page.AddRange(packet);
        }

        var bytes = page.ToArray();
        var crc = Le32(OggCrc(bytes));
        Buffer.BlockCopy(crc, 0, bytes, 22, 4);
        _sequence++;
        _stream.Write(bytes, 0, bytes.Length);
    }
}
