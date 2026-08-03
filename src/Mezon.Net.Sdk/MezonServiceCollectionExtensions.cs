using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mezon.Net.Sdk
{
    public static class MezonServiceCollectionExtensions
    {
        public static IServiceCollection AddMezonClient(this IServiceCollection services, Action<MezonClientOptions>? configure = null)
        {
            services.TryAddSingleton(provider =>
            {
                var options = new MezonClientOptions();
                configure?.Invoke(options);
                return new MezonClient(options);
            });
            return services;
        }

        [Obsolete("Use AddMezonClient instead.")]
        public static IServiceCollection AddMezonBotClient(this IServiceCollection services, Action<MezonClientOptions>? configure = null)
            => AddMezonClient(services, configure);
    }
}
