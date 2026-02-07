using System;
using Mezon.NET.DependencyInjection.Options;
using Microsoft.Extensions.Options;

namespace Mezon.NET
{
    public class MezonApiClientOptionsConfiguration : IConfigureNamedOptions<MezonApiClientOptions>
    {
        protected MezonClientOptions Options { get; private set; }

        public MezonApiClientOptionsConfiguration(IOptions<MezonClientOptions> options)
        {
            Options = options.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public void Configure(MezonApiClientOptions options)
        {
            Configure(string.Empty, options);
        }

        public void Configure(string name, MezonApiClientOptions options)
        {
            options.Host = Options.Host;
            options.Port = Options.Port;
            options.UseSSL = Options.UseSSL;
            options.GatewayBasePath = SetGatewayBasePath();
        }

        public string SetGatewayBasePath()
        {
            var scheme = Options.UseSSL ? "https" : "http";
            return $"{scheme}://{Options.Host}:{Options.Port}";
        }
    }
}
