using System.Net;
using System.Text;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Client.Tests.Helpers;

/// <summary>REST client double: answers each request with the bytes returned by <see cref="Responder"/> (null = 404).</summary>
internal sealed class FakeRestClient : IRestClient
{
    private int _callCount;

    public Func<string, string, byte[]?> Responder { get; set; } = (_, _) => null;
    public int ResponseDelayMs { get; set; }
    public int CallCount => Volatile.Read(ref _callCount);
    public Dictionary<string, string> Headers { get; } = new();

    public void SetHeader(string key, string value) => Headers[key] = value;

    public void SetCancelToken(CancellationToken cancelToken)
    {
    }

    public Task<HttpResponse> SendAsync(string method, string endpoint, CancellationToken cancelToken, bool headerOnly = false, IEnumerable<KeyValuePair<string, IEnumerable<string>>>? requestHeaders = null)
        => RespondAsync(method, endpoint);

    public Task<HttpResponse> SendAsync(string method, string endpoint, string json, CancellationToken cancelToken, bool headerOnly = false, IEnumerable<KeyValuePair<string, IEnumerable<string>>>? requestHeaders = null)
        => RespondAsync(method, endpoint);

    public Task<HttpResponse> SendAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartParams, CancellationToken cancelToken, bool headerOnly = false, IEnumerable<KeyValuePair<string, IEnumerable<string>>>? requestHeaders = null)
        => RespondAsync(method, endpoint);

    private async Task<HttpResponse> RespondAsync(string method, string endpoint)
    {
        Interlocked.Increment(ref _callCount);
        if (ResponseDelayMs > 0)
        {
            await Task.Delay(ResponseDelayMs);
        }

        var body = Responder(method, endpoint);
        return body == null
            ? new HttpResponse(HttpStatusCode.NotFound, new Dictionary<string, string>(), new MemoryStream())
            : new HttpResponse(HttpStatusCode.OK, new Dictionary<string, string>(), new MemoryStream(body));
    }

    public void Dispose()
    {
    }
}

internal static class TestJwt
{
    /// <summary>Unsigned JWT with the claims <see cref="Session"/> reads (exp, uid, usn).</summary>
    public static string Create(DateTimeOffset expiresAt, string userId = "1", string username = "bot")
    {
        var header = Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Encode($"{{\"exp\":{expiresAt.ToUnixTimeSeconds()},\"uid\":\"{userId}\",\"usn\":\"{username}\"}}");
        return $"{header}.{payload}.";
    }

    public static Mezon.Net.Internal.Api.Session CreateSession(TimeSpan lifetime, string sessionId)
        => new()
        {
            SessionId = sessionId,
            Token = Create(DateTimeOffset.UtcNow.Add(lifetime)),
            RefreshToken = Create(DateTimeOffset.UtcNow.AddDays(1)),
            TcpUrl = "127.0.0.1:9000",
            WsUrl = "127.0.0.1:9000",
        };

    private static string Encode(string json)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
