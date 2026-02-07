using System;
using System.Net;
using Mezon.NET.Core.Abstractions;

namespace Mezon.NET.Core
{
    public static class DefaultGRPCClientProvider
    {
        public static readonly GRPCClientProvider Instance = Create();

        /// <exception cref="PlatformNotSupportedException">The default GRPCClientProvider is not supported on this platform.</exception>
        public static GRPCClientProvider Create(bool useProxy = false, IWebProxy? webProxy = null)
        {
            return url =>
            {
                try
                {
                    return new DefaultGRPCClient(url, useProxy, webProxy);
                }
                catch (PlatformNotSupportedException ex)
                {
                    throw new PlatformNotSupportedException("The default GRPCClientProvider is not supported on this platform.", ex);
                }
            };
        }
    }
}
