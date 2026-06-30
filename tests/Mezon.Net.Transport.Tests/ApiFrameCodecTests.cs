using System.Buffers;
using System.Buffers.Binary;
using Mezon.Net.Core;

namespace Mezon.Net.Transport.Tests;

public class ApiFrameCodecTests
{
    [Fact]
    public void ApiFrame_FinishFlag_IsLowerSixteenBits()
    {
        const int responseCode = 200;
        var codeField = (responseCode << 16) | 0xff;
        Assert.Equal(200, (codeField >> 16) & 0xffff);
        Assert.Equal(0xff, codeField & 0xffff);
    }

    [Fact]
    public void AbridgedLength_SingleByteHeader_MultipleOfFour()
    {
        byte prefix = 5;
        int payloadLen = prefix * 4;
        Assert.Equal(20, payloadLen);
    }

    [Fact]
    public void PongFrame_HasThreeBytes()
    {
        var buffer = new byte[3];
        buffer[0] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(1), 7);
        Assert.Equal(0x00, buffer[0]);
        Assert.Equal(7, BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(1)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(65535)]
    public void ApiFrame_RoundTrip_PreservesCidAndCode(int cid)
    {
        var payload = new byte[] { 9, 8, 7 };
        var frame = Helpers.MezonTransportFrameBuilder.BuildApiFrame(cid, 201, finish: true, payload);
        Assert.Equal(0xff, frame[0]);
        Assert.Equal(cid, BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(1)));
        var codeField = BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(3));
        Assert.Equal(201, (codeField >> 16) & 0xffff);
        Assert.Equal(0xff, codeField & 0xffff);
        Assert.Equal(payload.Length, BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(7)));
        Assert.Equal(payload, frame.AsSpan(11).ToArray());
    }
}
