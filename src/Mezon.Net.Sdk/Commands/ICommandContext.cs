using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Models;
using Mezon.Net.Client;
using Mezon.Net.Sdk.Entities;

namespace Mezon.Net.Sdk.Commands
{
    public interface ICommandContext
    {
        MezonClient Client { get; }
        Message Message { get; }
        Channel Channel { get; }
        Clan? Clan { get; }
        User Author { get; }
        string Prefix { get; }
        string Name { get; }
        IReadOnlyList<string> Args { get; }
        CancellationToken CancellationToken { get; }

        Task<ChannelMessageAckResponse> ReplyAsync(MessageContent content, RequestOptions? options = null);
        Task<ChannelMessageAckResponse> ReplyTextAsync(string text, RequestOptions? options = null);
        Task<ChannelMessageAckResponse> SendAsync(MessageContent content, RequestOptions? options = null);
        Task<ChannelMessageAckResponse> SendTextAsync(string text, RequestOptions? options = null);
    }
}
