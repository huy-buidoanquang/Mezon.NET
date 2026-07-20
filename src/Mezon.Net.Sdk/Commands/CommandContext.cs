using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Models;
using Mezon.Net.Client;
using Mezon.Net.Sdk.Entities;

namespace Mezon.Net.Sdk.Commands
{
    internal sealed class CommandContext : ICommandContext
    {
        public CommandContext(
            MezonClient client,
            Message message,
            TextChannel channel,
            Clan? clan,
            User author,
            string prefix,
            string name,
            IReadOnlyList<string> args,
            CancellationToken cancellationToken)
        {
            Client = client;
            Message = message;
            Channel = channel;
            Clan = clan;
            Author = author;
            Prefix = prefix;
            Name = name;
            Args = args;
            CancellationToken = cancellationToken;
        }

        public MezonClient Client { get; }
        public Message Message { get; }
        public TextChannel Channel { get; }
        public Clan? Clan { get; }
        public User Author { get; }
        public string Prefix { get; }
        public string Name { get; }
        public IReadOnlyList<string> Args { get; }
        public CancellationToken CancellationToken { get; }

        public Task<ChannelMessageAckResponse> ReplyAsync(MessageContent content, RequestOptions? options = null)
            => Message.ReplyAsync(content, options: options);

        public Task<ChannelMessageAckResponse> ReplyTextAsync(string text, RequestOptions? options = null)
            => Message.ReplyTextAsync(text, options: options);

        public Task<ChannelMessageAckResponse> SendAsync(MessageContent content, RequestOptions? options = null)
            => Channel.SendAsync(content, options: options);

        public Task<ChannelMessageAckResponse> SendTextAsync(string text, RequestOptions? options = null)
            => Channel.SendTextAsync(text, options: options);
    }
}
