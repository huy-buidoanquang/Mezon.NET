using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Models;
using Mezon.Net.Client;
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
    }

    internal sealed class InteractionContext : IInteractionContext
    {
        public InteractionContext(
            MezonClient client,
            IInteraction interaction,
            Channel channel,
            Message? message,
            User user,
            CancellationToken cancellationToken)
        {
            Client = client;
            Interaction = interaction;
            Channel = channel;
            Message = message;
            User = user;
            CancellationToken = cancellationToken;
        }

        public MezonClient Client { get; }
        public IInteraction Interaction { get; }
        public Channel Channel { get; }
        public Message? Message { get; }
        public User User { get; }
        public CancellationToken CancellationToken { get; }

        public Task<ChannelMessageAckResponse> RespondAsync(MessageContent content, RequestOptions? options = null)
        {
            if (Message is null)
            {
                return Channel.SendAsync(content, options: options);
            }

            return Message.ReplyAsync(content, options: options);
        }

        public Task<ChannelMessageAckResponse> RespondTextAsync(string text, RequestOptions? options = null)
            => RespondAsync(MessageContent.CreateText(text), options);

        public Task UpdateMessageAsync(MessageContent content, RequestOptions? options = null)
        {
            if (Message is null)
            {
                throw new InvalidOperationException("The source message is not available for update.");
            }

            return Message.UpdateAsync(content, options: options);
        }

        public Task UpdateMessageTextAsync(string text, RequestOptions? options = null)
            => UpdateMessageAsync(MessageContent.CreateText(text), options);

        public Task<ChannelMessageAckResponse> SendEphemeralAsync(MessageContent content, RequestOptions? options = null)
            => Channel.SendEphemeralAsync(content, User.Id, options: options);

        public Task<ChannelMessageAckResponse> SendEphemeralTextAsync(string text, RequestOptions? options = null)
            => SendEphemeralAsync(MessageContent.CreateText(text), options);
    }
}
