using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mezon.Net.Abstractions;
using Mezon.Net.Core;
using Mezon.Net.Utils;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    internal sealed class DefaultRestClient : IRestClient, IDisposable
    {
        private const int HR_SECURECHANNELFAILED = -2146233079;

        private readonly HttpClient _client;
        private readonly string _baseUrl;
        private readonly Uri _baseUri;

        private readonly JsonSerializer _errorDeserializer;
        private CancellationToken _cancelToken;
        private bool _isDisposed;

        public DefaultRestClient(string baseUrl, bool useProxy = false, IWebProxy? webProxy = null)
        {
            _baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
            _baseUri = new Uri(_baseUrl);

#pragma warning disable IDISP014
            _client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = false,
                UseProxy = useProxy,
                Proxy = webProxy
            });
#pragma warning restore IDISP014
            SetHeader("accept-encoding", "gzip, deflate");

            _cancelToken = CancellationToken.None;
            _errorDeserializer = Json.Serializer;
        }

        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _client.Dispose();
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void SetHeader(string key, string value)
        {
            _client.DefaultRequestHeaders.Remove(key);
            if (value != null)
            {
                _client.DefaultRequestHeaders.Add(key, value);
            }
        }

        public void SetCancelToken(CancellationToken cancelToken)
        {
            _cancelToken = cancelToken;
        }

        public async Task<HttpResponse> SendAsync(
            string method,
            string endpoint,
            CancellationToken cancelToken = default,
            bool headerOnly = false,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>>? requestHeaders = null)
        {
            Check.NotNullOrEmpty(method, nameof(method));
            Check.NotNullOrEmpty(endpoint, nameof(endpoint));
            if (!Uri.TryCreate(_baseUri, endpoint, out var uri))
            {
                throw new InvalidOperationException("Invalid endpoint.");
            }

            using (var restRequest = new HttpRequestMessage(GetMethod(method), uri))
            {
                if (requestHeaders != null)
                {
                    foreach (var header in requestHeaders)
                    {
                        restRequest.Headers.Add(header.Key, header.Value);
                    }
                }

                return await SendInternalAsync(restRequest, cancelToken, headerOnly).ConfigureAwait(false);
            }
        }

        public async Task<HttpResponse> SendAsync(
            string method,
            string endpoint,
            string json,
            CancellationToken cancelToken = default,
            bool headerOnly = false,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>>? requestHeaders = null)
        {
            Check.NotNullOrEmpty(method, nameof(method));
            Check.NotNullOrEmpty(endpoint, nameof(endpoint));
            Check.NotNullOrEmpty(json, nameof(json));
            if (!Uri.TryCreate(_baseUri, endpoint, out var uri))
            {
                throw new InvalidOperationException("Invalid endpoint.");
            }

            using (var restRequest = new HttpRequestMessage(GetMethod(method), uri))
            {
                if (requestHeaders != null)
                {
                    foreach (var header in requestHeaders)
                    {
                        restRequest.Headers.Add(header.Key, header.Value);
                    }
                }

                restRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return await SendInternalAsync(restRequest, cancelToken, headerOnly).ConfigureAwait(false);
            }
        }

        /// <exception cref="InvalidOperationException">Unsupported param type.</exception>
        public Task<HttpResponse> SendAsync(
            string method,
            string endpoint,
            IReadOnlyDictionary<string, object> multipartParams,
            CancellationToken cancelToken = default,
            bool headerOnly = false,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>>? requestHeaders = null)
        {
            Check.NotNullOrEmpty(method, nameof(method));
            Check.NotNullOrEmpty(endpoint, nameof(endpoint));

            if (!Uri.TryCreate(_baseUri, endpoint, out var uri))
            {
                throw new InvalidOperationException("Invalid endpoint.");
            }

            // HttpRequestMessage implements IDisposable but we do not need to dispose it as it merely disposes of its Content property,
            // which we can do as needed. And regarding that, we do not want to take responsibility for disposing of content provided by
            // the caller of this function, since it's possible that the caller wants to reuse it or is forced to reuse it because of a
            // 429 response. Therefore, by convention, we only dispose the content objects created in this function (if any).
            //
            // See this comment explaining why this is safe: https://github.com/aspnet/Security/issues/886#issuecomment-229181249
            // See also the source for HttpRequestMessage: https://github.com/microsoft/referencesource/blob/master/System/net/System/Net/Http/HttpRequestMessage.cs
#pragma warning disable IDISP004
            var restRequest = new HttpRequestMessage(GetMethod(method), uri);
#pragma warning restore IDISP004

            if (requestHeaders != null)
            {
                foreach (var header in requestHeaders)
                {
                    restRequest.Headers.Add(header.Key, header.Value);
                }
            }

            var content = new MultipartFormDataContent("Upload----" + DateTimeOffset.Now.ToString(CultureInfo.InvariantCulture));

            static StreamContent GetStreamContent(Stream stream)
            {
                if (stream.CanSeek)
                {
                    // Reset back to the beginning; it may have been used elsewhere or in a previous request.
                    stream.Position = 0;
                }

#pragma warning disable IDISP004
                return new StreamContent(stream);
#pragma warning restore IDISP004
            }

            foreach (var p in multipartParams ?? ImmutableDictionary<string, object>.Empty)
            {
                switch (p.Value)
                {
#pragma warning disable IDISP004
                    case string stringValue:
                    {
                        content.Add(new StringContent(stringValue, Encoding.UTF8, "text/plain"), p.Key);
                        continue;
                    }
                    case byte[] byteArrayValue:
                    {
                        content.Add(new ByteArrayContent(byteArrayValue), p.Key);
                        continue;
                    }
                    case Stream streamValue:
                    {
                        content.Add(GetStreamContent(streamValue), p.Key);
                        continue;
                    }
                    case MultipartFile fileValue:
                    {
                        var streamContent = GetStreamContent(fileValue.Stream);

                        if (fileValue.ContentType != null)
                        {
                            streamContent.Headers.ContentType = new MediaTypeHeaderValue(fileValue.ContentType);
                        }

                        content.Add(streamContent, p.Key, fileValue.Filename);
#pragma warning restore IDISP004

                        continue;
                    }
                    default:
                        throw new InvalidOperationException($"Unsupported param type \"{p.Value.GetType().Name}\".");
                }
            }

            restRequest.Content = content;
            return SendInternalAsync(restRequest, cancelToken, headerOnly);
        }

        //        public async Task<HttpResponse> SendAsync(
        //            string method,
        //            string endpoint,
        //            CancellationToken cancelToken,
        //            bool headerOnly,
        //            string reason = null,
        //            IEnumerable<KeyValuePair<string, IEnumerable<string>>> requestHeaders = null)
        //        {
        //            string uri = BuildFullUri(_baseUrl, endpoint).ToString();
        //            using (var restRequest = new HttpRequestMessage(GetMethod(method), uri))
        //            {
        //                if (reason != null)
        //                    restRequest.Headers.Add("X-Audit-Log-Reason", Uri.EscapeDataString(reason));
        //                if (requestHeaders != null)
        //                    foreach (var header in requestHeaders)
        //                        restRequest.Headers.Add(header.Key, header.Value);
        //                return await SendInternalAsync(restRequest, cancelToken, headerOnly).ConfigureAwait(false);
        //            }
        //        }
        //        public async Task<HttpResponse> SendAsync(
        //            string method,
        //            string endpoint,
        //            string json,
        //            CancellationToken cancelToken,
        //            bool headerOnly,
        //            string? reason = null,
        //            IEnumerable<KeyValuePair<string, IEnumerable<string>>> requestHeaders = null)
        //        {
        //            string uri = Path.Combine(_baseUrl, endpoint);
        //            using (var restRequest = new HttpRequestMessage(GetMethod(method), uri))
        //            {
        //                if (reason != null)
        //                    restRequest.Headers.Add("X-Audit-Log-Reason", Uri.EscapeDataString(reason));
        //                if (requestHeaders != null)
        //                    foreach (var header in requestHeaders)
        //                        restRequest.Headers.Add(header.Key, header.Value);
        //                restRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        //                return await SendInternalAsync(restRequest, cancelToken, headerOnly).ConfigureAwait(false);
        //            }
        //        }

        //        /// <exception cref="InvalidOperationException">Unsupported param type.</exception>
        //        public Task<HttpResponse> SendAsync(
        //            string method,
        //            string endpoint,
        //            IReadOnlyDictionary<string, object> multipartParams,
        //            CancellationToken cancelToken,
        //            bool headerOnly,
        //            string reason = null,
        //            IEnumerable<KeyValuePair<string, IEnumerable<string>>> requestHeaders = null)
        //        {
        //            string uri = Path.Combine(_baseUrl, endpoint);

        //            // HttpRequestMessage implements IDisposable but we do not need to dispose it as it merely disposes of its Content property,
        //            // which we can do as needed. And regarding that, we do not want to take responsibility for disposing of content provided by
        //            // the caller of this function, since it's possible that the caller wants to reuse it or is forced to reuse it because of a
        //            // 429 response. Therefore, by convention, we only dispose the content objects created in this function (if any).
        //            //
        //            // See this comment explaining why this is safe: https://github.com/aspnet/Security/issues/886#issuecomment-229181249
        //            // See also the source for HttpRequestMessage: https://github.com/microsoft/referencesource/blob/master/System/net/System/Net/Http/HttpRequestMessage.cs
        //#pragma warning disable IDISP004
        //            var restRequest = new HttpRequestMessage(GetMethod(method), uri);
        //#pragma warning restore IDISP004

        //            if (reason != null)
        //                restRequest.Headers.Add("X-Audit-Log-Reason", Uri.EscapeDataString(reason));
        //            if (requestHeaders != null)
        //                foreach (var header in requestHeaders)
        //                    restRequest.Headers.Add(header.Key, header.Value);
        //            var content = new MultipartFormDataContent("Upload----" + DateTimeOffset.Now.ToString(CultureInfo.InvariantCulture));

        //            static StreamContent GetStreamContent(Stream stream)
        //            {
        //                if (stream.CanSeek)
        //                {
        //                    // Reset back to the beginning; it may have been used elsewhere or in a previous request.
        //                    stream.Position = 0;
        //                }

        //#pragma warning disable IDISP004
        //                return new StreamContent(stream);
        //#pragma warning restore IDISP004
        //            }

        //            foreach (var p in multipartParams ?? ImmutableDictionary<string, object>.Empty)
        //            {
        //                switch (p.Value)
        //                {
        //#pragma warning disable IDISP004
        //                    case string stringValue:
        //                        { content.Add(new StringContent(stringValue, Encoding.UTF8, "text/plain"), p.Key); continue; }
        //                    case byte[] byteArrayValue:
        //                        { content.Add(new ByteArrayContent(byteArrayValue), p.Key); continue; }
        //                    case Stream streamValue:
        //                        { content.Add(GetStreamContent(streamValue), p.Key); continue; }
        //                    case MultipartFile fileValue:
        //                        {
        //                            var streamContent = GetStreamContent(fileValue.Stream);

        //                            if (fileValue.ContentType != null)
        //                                streamContent.Headers.ContentType = new MediaTypeHeaderValue(fileValue.ContentType);

        //                            content.Add(streamContent, p.Key, fileValue.Filename);
        //#pragma warning restore IDISP004

        //                            continue;
        //                        }
        //                    default:
        //                        throw new InvalidOperationException($"Unsupported param type \"{p.Value.GetType().Name}\".");
        //                }
        //            }

        //            restRequest.Content = content;
        //            return SendInternalAsync(restRequest, cancelToken, headerOnly);
        //        }

        //        public async Task<HttpResponse> SendAsync(
        //            string method,
        //            string endpoint,
        //            IEnumerable<KeyValuePair<string, IEnumerable<string>>> requestHeaders = null,
        //            Dictionary<string, object> queryParams = null,
        //            string json = null,
        //            int timeoutInMilliseconds = 10000,
        //            CancellationToken cancellationToken = default)
        //        {
        //            Check.NotNullOrEmpty(method, nameof(method));
        //            var fullUri = BuildFullUri(_baseUrl, endpoint, queryParams);

        //            using (var request = new HttpRequestMessage(GetMethod(method), fullUri))
        //            {

        //                if (requestHeaders != null)
        //                {
        //                    BuildHttpHeader(request, requestHeaders);
        //                }

        //                if (json != null)
        //                {
        //                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        //                }

        //                using var timeoutCts = new CancellationTokenSource(timeoutInMilliseconds);
        //                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        //                try
        //                {
        //                    return await SendInternalAsync(request, linkedCts.Token, headerOnly: false).ConfigureAwait(false);
        //                }
        //                catch (TaskCanceledException) when (timeoutCts.IsCancellationRequested)
        //                {
        //                    throw new TimeoutException("Request timed out.");
        //                }
        //                catch (Exception)
        //                {
        //                    throw;
        //                }
        //            }
        //        }

        private async Task<HttpResponse> SendInternalAsync(
            HttpRequestMessage request,
            CancellationToken cancelToken,
            bool headerOnly = false)
        {
            using (var cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancelToken, cancelToken))
            {
                cancelToken = cancelTokenSource.Token;
                HttpResponseMessage response = await _client.SendAsync(request, cancelToken).ConfigureAwait(false);

                var headers = response.Headers.ToDictionary(x => x.Key, x => x.Value.FirstOrDefault(), StringComparer.OrdinalIgnoreCase);
                var stream = (!headerOnly || !response.IsSuccessStatusCode) ? await response.Content.ReadAsStreamAsync().ConfigureAwait(false) : null;

                return new HttpResponse(response.StatusCode, headers!, stream ?? Stream.Null);
            }
        }

        private static readonly HttpMethod Patch = new HttpMethod("PATCH");

        private Uri BuildFullUri(
            [NotNull] string baseUrl,
            string relativeUrl,
            Dictionary<string, object>? queryParams = null)
        {
            Check.NotNullOrEmpty(baseUrl, nameof(baseUrl));
            Check.NotNullOrEmpty(relativeUrl, nameof(relativeUrl));

            var apiUri = new Uri(new Uri(baseUrl), relativeUrl);
            var builder = new UriBuilder(apiUri);
            if (queryParams != null && queryParams.Count > 0)
            {
                var queryString = new StringBuilder();
                foreach (var param in queryParams)
                {
                    if (!string.IsNullOrEmpty(param.Key) && param.Value != null)
                    {
                        if (queryString.Length > 0)
                        {
                            queryString.Append('&');
                        }

                        var key = WebUtility.UrlEncode(param.Key);
                        var value = param.Value != null ? WebUtility.UrlEncode(param.Value.ToString()) : string.Empty;

                        queryString.Append($"{key}={value}");
                    }
                }

                builder.Query = queryString.ToString();
            }

            return builder.Uri;
        }

        private HttpMethod GetMethod(string method)
        {
            return method switch
            {
                "DELETE" => HttpMethod.Delete,
                "GET" => HttpMethod.Get,
                "PATCH" => Patch,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                _ => throw new ArgumentOutOfRangeException(nameof(method), $"Unknown HttpMethod: {method}"),
            };
        }

        private static void BuildHttpHeader(HttpRequestMessage httpRequestMessage, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            Check.NotNull(httpRequestMessage, nameof(httpRequestMessage));
            if (headers is null)
            {
                return;
            }

            foreach (var header in headers)
            {
                httpRequestMessage.Headers.Add(header.Key, header.Value);
            }
        }

        public Task<T> SendRequestAsync<T>(string urlPath, HttpMethod method, string? bearerToken = null, string? basicAuthUsername = null, string? basicAuthPassword = null, object? body = null, Dictionary<string, object>? queryParams = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
