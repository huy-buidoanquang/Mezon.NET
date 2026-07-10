using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Mmn.Models;
using Mezon.Net.Mmn.Utils;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Mezon.Net.Mmn
{
    public sealed class MmnClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly Uri _baseUri;
        private readonly int _timeoutMs;

        public MmnClient(string baseUrl, int timeoutMs = 7000, HttpClient? httpClient = null)
        {
            _baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
            _timeoutMs = timeoutMs;
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
        }

        public EphemeralKeyPair GenerateEphemeralKeyPair()
        {
            var generator = new Ed25519KeyPairGenerator();
            generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
            var pair = generator.GenerateKeyPair();
            var publicKey = Convert.ToBase64String(((Ed25519PublicKeyParameters)pair.Public).GetEncoded());
            var privateBytes = ((Ed25519PrivateKeyParameters)pair.Private).GetEncoded();
            var privateKey = BitConverter.ToString(privateBytes).Replace("-", string.Empty, StringComparison.Ordinal);
            return new EphemeralKeyPair { PublicKey = publicKey, PrivateKey = privateKey };
        }

        public string GetAddressFromUserId(long userId) => Base58Encoder.AddressFromUserId(userId);

        public string ScaleAmountToDecimals(string amount, int decimals = 18)
        {
            if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                value = 0;
            }

            var scaled = value * (decimal)Math.Pow(10, decimals);
            return scaled.ToString("0", CultureInfo.InvariantCulture);
        }

        public async Task<NonceResult> GetCurrentNonceAsync(string address, string tag = "pending", CancellationToken cancellationToken = default)
        {
            var response = await PostJsonRpcAsync("eth_getTransactionCount", new object[] { address, tag }, cancellationToken).ConfigureAwait(false);
            var nonceHex = response.GetProperty("result").GetString() ?? "0x0";
            var nonce = Convert.ToInt64(nonceHex.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase), 16);
            return new NonceResult { Nonce = nonce };
        }

        public async Task<SendTransactionResult> SendTransactionAsync(SendTransactionRequest request, CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                sender = request.Sender,
                recipient = request.Recipient,
                amount = request.Amount,
                nonce = request.Nonce,
                textData = request.TextData,
                extraInfo = request.ExtraInfo,
                publicKey = request.PublicKey,
                privateKey = request.PrivateKey,
                zkProof = request.ZkProof,
                zkPub = request.ZkPub,
            };

            try
            {
                using var response = await _http.PostAsync(
                    new Uri(_baseUri, "sendTransaction"),
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                    cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                return new SendTransactionResult
                {
                    Ok = root.TryGetProperty("ok", out var ok) && ok.GetBoolean(),
                    TxHash = root.TryGetProperty("tx_hash", out var hash) ? hash.GetString() : null,
                    Error = root.TryGetProperty("error", out var error) ? error.GetString() : null,
                };
            }
            catch (Exception ex)
            {
                return new SendTransactionResult { Ok = false, Error = ex.Message };
            }
        }

        private async Task<JsonElement> PostJsonRpcAsync(string method, object[] parameters, CancellationToken cancellationToken)
        {
            var payload = new
            {
                jsonrpc = "2.0",
                id = 1,
                method,
                @params = parameters,
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeoutMs);
            using var response = await _http.PostAsync(
                _baseUri,
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }

        public void Dispose() => _http.Dispose();
    }
}
