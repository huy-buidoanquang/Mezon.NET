using System.Buffers.Binary;

namespace Mezon.Net.Sdk.Sfu.Tests;

public class OggOpusTests
{
    [Fact]
    public void Crc_matches_the_ogg_parameters()
    {
        // CRC-32/POSIX without its final inversion (poly 0x04C11DB7, init 0, not reflected).
        Assert.Equal(0x765E_7680u ^ 0xFFFF_FFFFu, OggOpusWriter.OggCrc("123456789"u8));
    }

    [Fact]
    public void Packet_durations()
    {
        foreach (var (packet, micros) in new (byte[], long)[]
                 {
                     (new byte[] { 0xF8 }, 20_000), (new byte[] { 0x80 }, 2_500), (new byte[] { 0x18 }, 60_000), (new byte[] { 0x61 }, 20_000),
                     (new byte[] { 0xFB, 0x03 }, 60_000), (new byte[] { 0x0A }, 40_000),
                 })
        {
            Assert.Equal(TimeSpan.FromTicks(micros * 10), OpusPacket.Duration(packet));
        }

        Assert.Throws<InvalidDataException>(() => OpusPacket.Duration(Array.Empty<byte>()));
        Assert.Throws<InvalidDataException>(() => OpusPacket.Duration(new byte[] { 0xFB }));
    }

    private static byte[] Frame(int i)
    {
        var p = new byte[40 + i % 300];
        p[0] = 0xF8;
        for (var j = 1; j < p.Length; j++)
        {
            p[j] = (byte)(i + j);
        }

        return p;
    }

    [Fact]
    public void Round_trip_with_valid_checksums()
    {
        var buffer = new MemoryStream();
        var writer = new OggOpusWriter(buffer, 2);
        for (var i = 0; i < 100; i++)
        {
            writer.WritePacket(Frame(i));
        }

        writer.Finish();
        writer.Finish();
        var bytes = buffer.ToArray();
        var at = 0;
        var pages = 0;
        ulong lastGranule = 0;
        while (at < bytes.Length)
        {
            var segments = bytes[at + 26];
            var size = 27 + segments;
            for (var s = 0; s < segments; s++)
            {
                size += bytes[at + 27 + s];
            }

            var page = bytes.AsSpan(at, size).ToArray();
            var stored = BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(22));
            page.AsSpan(22, 4).Clear();
            Assert.Equal(stored, OggOpusWriter.OggCrc(page));
            lastGranule = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(at + 6));
            at += size;
            pages++;
        }

        Assert.Equal(103, pages);
        Assert.Equal(100UL * 960, lastGranule);
        var reader = new OggOpusReader(new MemoryStream(bytes));
        Assert.Equal(2, reader.Head.Channels);
        Assert.Equal(48_000u, reader.Head.SampleRate);
        for (var i = 0; i < 100; i++)
        {
            var (packet, duration) = reader.ReadPacket()!.Value;
            Assert.Equal(Frame(i), packet);
            Assert.Equal(TimeSpan.FromMilliseconds(20), duration);
        }

        Assert.Null(reader.ReadPacket());
    }

    private static byte[] Page(byte[] segments, byte[] body)
    {
        var p = new List<byte>("OggS"u8.ToArray());
        p.AddRange(new byte[22]);
        p.Add((byte)segments.Length);
        p.AddRange(segments);
        p.AddRange(body);
        return p.ToArray();
    }

    [Fact]
    public void Packets_spanning_pages_are_reassembled()
    {
        var head = new List<byte>("OpusHead"u8.ToArray());
        head.AddRange(new byte[] { 1, 1, 0, 0, 0x80, 0xBB, 0, 0, 0, 0, 0 });
        var tags = "OpusTags"u8.ToArray().Concat(new byte[8]).ToArray();
        var big = Enumerable.Repeat((byte)0xF8, 300).ToArray();
        var stream = new List<byte>();
        stream.AddRange(Page(new byte[] { 19 }, head.ToArray()));
        stream.AddRange(Page(new byte[] { 16 }, tags));
        stream.AddRange(Page(new byte[] { 255 }, big.Take(255).ToArray()));
        stream.AddRange(Page(new byte[] { 45, 3 }, big.Skip(255).Concat(new byte[] { 0xF8, 1, 2 }).ToArray()));
        var reader = new OggOpusReader(new MemoryStream(stream.ToArray()));
        Assert.Equal(big, reader.ReadPacket()!.Value.Packet);
        Assert.Equal(3, reader.ReadPacket()!.Value.Packet.Length);
        Assert.Null(reader.ReadPacket());
        Assert.Throws<InvalidDataException>(() => new OggOpusReader(new MemoryStream(Page(new byte[] { 4 }, "abcd"u8.ToArray()))));
    }
}
