using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Client;
using Mezon.Net.Core;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Entities;

namespace Mezon.Net.Sdk.Interactions
{
    public delegate Task InteractionHandler(IInteractionContext context);

    public interface IInteractionContext
    {
        MezonClient Client { get; }
        IInteraction Interaction { get; }
        Channel Channel { get; }
        Message? Message { get; }
        User User { get; }
        CancellationToken CancellationToken { get; }

        Task<ChannelMessageAckResponse> RespondAsync(MessageContent content, RequestOptions? options = null);
        Task<ChannelMessageAckResponse> RespondTextAsync(string text, RequestOptions? options = null);
        Task UpdateMessageAsync(MessageContent content, RequestOptions? options = null);
        Task UpdateMessageTextAsync(string text, RequestOptions? options = null);
        Task<ChannelMessageAckResponse> SendEphemeralAsync(MessageContent content, RequestOptions? options = null);
        Task<ChannelMessageAckResponse> SendEphemeralTextAsync(string text, RequestOptions? options = null);
        Task<ChannelMessageAckResponse> UpdateEphemeralAsync(long messageId, MessageContent content, RequestOptions? options = null);
        Task<ChannelMessageAckResponse> UpdateEphemeralTextAsync(long messageId, string text, RequestOptions? options = null);
        Task<ChannelMessageAckResponse> DeleteEphemeralAsync(long messageId, RequestOptions? options = null);
    }
}
