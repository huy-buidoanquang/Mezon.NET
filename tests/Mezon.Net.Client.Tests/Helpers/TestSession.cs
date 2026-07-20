using Mezon.Net.Abstractions;

namespace Mezon.Net.Client.Tests.Helpers;

internal sealed class TestSession : ISession
{
    public TestSession(string sessionId, string tcpUrl)
    {
        SessionId = sessionId;
        AuthToken = sessionId;
        TcpUrl = tcpUrl;
        WsUrl = tcpUrl;
    }

    public string SessionId { get; }
    public string AuthToken { get; }
    public string RefreshToken => AuthToken;
    public bool Created => true;
    public long CreatedAt => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long ExpiresAt => DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
    public long RefreshExpiresAt => ExpiresAt;
    public string? Username => "test";
    public string? UserId => "1";
    public bool IsRemember => false;
    public string? ApiUrl => "http://127.0.0.1:8088";
    public string? IdToken => null;
    public string? WsUrl { get; }
    public string? TcpUrl { get; }

    public bool IsExpiredSoon(int seconds) => false;
    public bool IsRefreshExpiredSoon(int seconds) => false;
    public bool IsExpired() => false;
    public bool IsRefreshExpired() => false;
}
