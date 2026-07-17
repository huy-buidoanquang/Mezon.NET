using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mezon.Net.Mmn.Models;

namespace Mezon.Net.Mmn
{
    public sealed class ProveResponseData
    {
        [JsonPropertyName("proof")]
        public string? Proof { get; set; }

        [JsonPropertyName("public_input")]
        public string? PublicInput { get; set; }
    }

    public sealed class ProveResponse
    {
        [JsonPropertyName("data")]
        public ProveResponseData? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public sealed class ZkProveClient : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private bool _disposed;

        public ZkProveClient(string endpoint, int timeoutMs = 7000, HttpClient? httpClient = null)
        {
            _endpoint = endpoint.TrimEnd('/');
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<ProveResponse> GenerateZkProofAsync(
            string userId,
            string address,
            string ephemeralPk,
            string jwt,
            CancellationToken cancellationToken = default)
        {
            var url = $"{_endpoint}/prove";
            var requestBody = new Dictionary<string, string>
            {
                ["user_id"] = userId,
                ["address"] = address,
                ["ephemeral_pk"] = ephemeralPk,
                ["jwt"] = jwt,
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Prove request failed: {(int)response.StatusCode} {response.ReasonPhrase} - {responseBody}");
            }

            return JsonSerializer.Deserialize<ProveResponse>(responseBody, JsonOptions)
                ?? throw new InvalidOperationException("Prove response was empty.");
        }

        public async Task<ZkHealthCheckResponse> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var url = $"{_endpoint}/health/check";
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Health check failed: {(int)response.StatusCode} {response.ReasonPhrase} - {responseBody}");
            }

            return JsonSerializer.Deserialize<ZkHealthCheckResponse>(responseBody, JsonOptions)
                ?? new ZkHealthCheckResponse();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
