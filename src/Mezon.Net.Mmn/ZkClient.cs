using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Mmn.Models;

namespace Mezon.Net.Mmn
{
    public sealed class ZkClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly Uri _endpoint;
        private readonly int _timeoutMs;

        public ZkClient(string endpoint, int timeoutMs = 7000, HttpClient? httpClient = null)
        {
            _endpoint = new Uri(endpoint.TrimEnd('/') + "/");
            _timeoutMs = timeoutMs;
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
        }

        public async Task<ZkProofResult> GetZkProofsAsync(ZkProofRequest request, CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                userId = request.UserId,
                jwt = request.Jwt,
                address = request.Address,
                ephemeralPublicKey = request.EphemeralPublicKey,
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeoutMs);
            using var response = await _http.PostAsync(
                new Uri(_endpoint, "zk-proof"),
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new ZkProofResult
            {
                Proof = root.TryGetProperty("proof", out var proof) ? proof.GetString() ?? string.Empty : string.Empty,
                PublicInput = root.TryGetProperty("public_input", out var pub) ? pub.GetString() ?? string.Empty : string.Empty,
            };
        }

        public void Dispose() => _http.Dispose();
    }
}
