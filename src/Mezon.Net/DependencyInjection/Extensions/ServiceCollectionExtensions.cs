using Mezon.NET.Abstractions;
using Mezon.NET.Abstractions.Managers;
using Mezon.NET.DependencyInjection.Options;
using Mezon.NET.Managers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mezon.NET.DependencyInjection.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>  
        /// Configures Mezon Api Client services.
        /// </summary>  
        /// <param name="services">The service collection.</param>  
        /// <param name="configuration">The application configuration.</param>  
        /// <returns>The updated service collection.</returns>  
        public static IServiceCollection ConfigureMezonApiClient(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<MezonClientOptions>()
                .Bind(configuration.GetSection(nameof(MezonClientOptions)))
                .ValidateDataAnnotations();

            services.AddSingleton<IConfigureOptions<MezonApiClientOptions>, MezonApiClientOptionsConfiguration>();
            services.AddHttpClient<IMezonApiClient, MezonApiClient>();

            services.ConfigureMezonSocket();

            // temp
            services.AddSingleton<MezonClient>();

            services.AddSingleton<ISessionManager, SessionManager>();
            services.AddSingleton<IChannelManager, ChannelManager>();
            return services;
        }

        private static IServiceCollection ConfigureMezonSocket(this IServiceCollection services)
        {
            services.AddTransient<WebSocketAdapterText>();
            services.AddTransient<WebSocketAdapterProtobuf>();
            services.AddSingleton<IWebSocketAdapterFactory, WebSocketAdapterFactory>();
            services.AddSingleton<ISocketManager, SocketManager>();

            return services;
        }
    }
}
