using System;
using System.Threading.Tasks;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Internal.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mezon.Net.Sdk
{
    /// <summary>
    /// High-level bot/application facade over <see cref="MezonClient"/>.
    /// </summary>
    public sealed class MezonBotClient : MezonClient
    {
        public MezonBotClient() : base(new MezonSocketClientOptions())
        {
        }

        public MezonBotClient(MezonSocketClientOptions options) : base(options)
        {
        }

        public Task<Envelope> SendChannelMessageAsync(
            long clanId,
            long channelId,
            string content,
            RequestOptions? options = null)
        {
            var envelope = new Envelope
            {
                ChannelMessageSend = new ChannelMessageSend
                {
                    ClanId = clanId,
                    ChannelId = channelId,
                    Content = content,
                }
            };
            return SendRealtimeAsync(envelope, options);
        }

        public Task<Envelope> ReplyAsync(
            long clanId,
            long channelId,
            string content,
            RequestOptions? options = null)
            => SendChannelMessageAsync(clanId, channelId, content, options);
    }

    public static class MezonServiceCollectionExtensions
    {
        public static IServiceCollection AddMezonBotClient(this IServiceCollection services, Action<MezonSocketClientOptions>? configure = null)
        {
            services.TryAddSingleton(_ =>
            {
                var options = new MezonSocketClientOptions();
                configure?.Invoke(options);
                return new MezonBotClient(options);
            });
            return services;
        }
    }
}
