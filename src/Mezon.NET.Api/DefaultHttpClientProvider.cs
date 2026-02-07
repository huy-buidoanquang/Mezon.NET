using System;
using System.Net;
using Mezon.NET.Core.Abstractions;

namespace Mezon.NET.Api
{
    public static class DefaultHttpClientProvider
    {
        public static readonly HttpClientProvider Instance = Create();

        /// <exception cref="PlatformNotSupportedException">The default HttpClientProvider is not supported on this platform.</exception>
        public static HttpClientProvider Create(bool useProxy = false, IWebProxy? webProxy = null)
        {
            return url =>
            {
                try
                {
                    return new DefaultHttpClient(url, useProxy, webProxy);
                }
                catch (PlatformNotSupportedException ex)
                {
                    throw new PlatformNotSupportedException("The default HttpClientProvider is not supported on this platform.", ex);
                }
            };
        }
    }
}
