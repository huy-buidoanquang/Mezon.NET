using Mezon.Net.Core;

namespace Mezon.Net.Client.Tests;

public sealed class MezonApiExceptionTests
{
    [Theory]
    [InlineData(403, MezonStatusCode.Unknown)]
    [InlineData(503, MezonStatusCode.Unknown)]
    [InlineData(7, MezonStatusCode.PermissionDenied)]
    public void FromSocketResponse_keeps_the_code_the_server_sent(int code, MezonStatusCode expected)
    {
        var ex = MezonApiException.FromSocketResponse(code, "GetStreamPlayback", "Permission denied."u8.ToArray());

        Assert.Equal(expected, ex.StatusCode);
        Assert.Equal(code, ex.RawCode);
        Assert.Equal("Permission denied.", ex.Detail);
    }

    [Fact]
    public void Constructed_exception_uses_its_status_code_as_raw_code()
    {
        Assert.Equal((int)MezonStatusCode.NotFound, new MezonApiException(MezonStatusCode.NotFound).RawCode);
    }
}
