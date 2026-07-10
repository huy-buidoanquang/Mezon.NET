using System;
using System.Net;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Client
{
    public static class DefaultRestClientProvider
    {
        public static readonly RestClientProvider Instance = Create();

        /// <exception cref="PlatformNotSupportedException">The default RestClientProvider is not supported on this platform.</exception>
        public static RestClientProvider Create(bool useProxy = false, IWebProxy? webProxy = null)
        {
            return url =>
            {
                try
                {
                    return new DefaultRestClient(url, useProxy, webProxy);
                }
                catch (PlatformNotSupportedException ex)
                {
                    throw new PlatformNotSupportedException("The default RestClientProvider is not supported on this platform.", ex);
                }
            };
        }
    }
}
