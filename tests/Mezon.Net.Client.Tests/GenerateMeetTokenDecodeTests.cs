using System.Text;
using Google.Protobuf;
using Mezon.Net.Internal.Api;

namespace Mezon.Net.Client.Tests;

public sealed class GenerateMeetTokenDecodeTests
{
    [Fact]
    public void Protobuf_reply_is_parsed()
    {
        var payload = new GenerateMeetTokenResponse { Token = "eyJ.token.sig", Url = "wss://sfu.example" }.ToByteArray();

        var response = MezonSocketClient.DecodeGenerateMeetTokenResponse(payload);

        Assert.Equal("eyJ.token.sig", response.Token);
        Assert.Equal("wss://sfu.example", response.Url);
    }

    [Fact]
    public void Raw_jwt_reply_from_older_servers_is_decoded_as_text()
    {
        var payload = Encoding.UTF8.GetBytes("eyJhbGciOiJIUzI1NiJ9.e30.sig");

        var response = MezonSocketClient.DecodeGenerateMeetTokenResponse(payload);

        Assert.Equal("eyJhbGciOiJIUzI1NiJ9.e30.sig", response.Token);
    }

    [Fact]
    public void Empty_reply_yields_empty_token()
    {
        var response = MezonSocketClient.DecodeGenerateMeetTokenResponse(ReadOnlySpan<byte>.Empty);

        Assert.Equal(string.Empty, response.Token);
    }

    [Fact]
    public void Invalid_protobuf_falls_back_to_text()
    {
        byte[] payload = [0x0A, 0x7F, 0x41];

        var response = MezonSocketClient.DecodeGenerateMeetTokenResponse(payload);

        Assert.Equal(Encoding.UTF8.GetString(payload), response.Token);
    }
}
