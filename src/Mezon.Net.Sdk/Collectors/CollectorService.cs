using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Entities;
using Mezon.Net.Sdk.Interactions;

namespace Mezon.Net.Sdk.Collectors
{
    public sealed class CollectorService
    {
        private readonly List<MessageCollectorSession> _messageCollectors = new List<MessageCollectorSession>();
        private readonly List<ComponentCollectorSession> _componentCollectors = new List<ComponentCollectorSession>();
        private readonly object _gate = new object();
        private MezonClient? _client;

        public MezonClient Attach(MezonClient client)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (_client is not null)
            {
                throw new InvalidOperationException("CollectorService is already attached to a client.");
            }

            _client = client;
            ClientInteractionHub.GetOrCreate(client).RegisterCollector(this);
            return client;
        }

        public void Detach()
        {
            if (_client is null)
            {
                return;
            }

            CancelAllCollectors();
            ClientInteractionHub.GetOrCreate(_client).UnregisterCollector(this);
            _client = null;
        }

        public Task<MessageCollectorResult> CollectMessageAsync(
            MessageCollectorOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (_client is null)
            {
                throw new InvalidOperationException("CollectorService is not attached to a client.");
            }

            MessageCollectorSession session;
            lock (_gate)
            {
                session = new MessageCollectorSession(options, cancellationToken, UnregisterMessageCollector);
                _messageCollectors.Add(session);
            }

            return session.Task;
        }

        public Task<ComponentCollectorResult> CollectComponentAsync(
            ComponentCollectorOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (_client is null)
            {
                throw new InvalidOperationException("CollectorService is not attached to a client.");
            }

            ComponentCollectorSession session;
            lock (_gate)
            {
                session = new ComponentCollectorSession(options, cancellationToken, UnregisterComponentCollector);
                _componentCollectors.Add(session);
            }

            return session.Task;
        }

        internal async Task<bool> TryDispatchMessageAsync(MezonClient client, ChannelMessageEventData eventData)
        {
            MessageCollectorSession[] sessions;
            lock (_gate)
            {
                if (_messageCollectors.Count == 0)
                {
                    return false;
                }

                sessions = _messageCollectors.ToArray();
            }

            var data = (ChannelMessageResponse)eventData;
            Message? message = null;
            if (client.Channels.TryGet(data.ChannelId, out var channel))
            {
                if (channel.Messages.TryGet(data.MessageId, out var cached))
                {
                    message = cached;
                }
                else
                {
                    message = new Message(client, channel, data);
                    channel.Messages.Set(data.MessageId, message);
                }
            }
            else
            {
                return false;
            }

            var consumed = false;
            for (var i = 0; i < sessions.Length; i++)
            {
                if (sessions[i].TryCollect(message))
                {
                    consumed = true;
                }
            }

            return consumed;
        }

        internal Task<bool> TryDispatchButtonAsync(MezonClient client, MessageButtonClickedEventData eventData)
        {
            var data = (MessageButtonClickedResponse)eventData;
            var interaction = new ButtonInteraction(
                data.MessageId,
                data.ChannelId,
                data.ButtonId,
                data.UserId,
                data.SenderId,
                data.ExtraData);
            return TryDispatchComponentAsync(interaction);
        }

        internal Task<bool> TryDispatchSelectAsync(MezonClient client, DropdownBoxSelectedEventData eventData)
        {
            var data = (DropdownBoxSelectedResponse)eventData;
            var values = new string[data.Values.Count];
            for (var i = 0; i < data.Values.Count; i++)
            {
                values[i] = data.Values[i];
            }

            var interaction = new SelectInteraction(
                data.MessageId,
                data.ChannelId,
                data.SelectboxId,
                data.UserId,
                data.SenderId,
                values);
            return TryDispatchComponentAsync(interaction);
        }

        private Task<bool> TryDispatchComponentAsync(IInteraction interaction)
        {
            ComponentCollectorSession[] sessions;
            lock (_gate)
            {
                if (_componentCollectors.Count == 0)
                {
                    return Task.FromResult(false);
                }

                sessions = _componentCollectors.ToArray();
            }

            var consumed = false;
            for (var i = 0; i < sessions.Length; i++)
            {
                if (sessions[i].TryCollect(interaction))
                {
                    consumed = true;
                }
            }

            return Task.FromResult(consumed);
        }

        private void UnregisterMessageCollector(MessageCollectorSession session)
        {
            lock (_gate)
            {
                _messageCollectors.Remove(session);
            }
        }

        private void UnregisterComponentCollector(ComponentCollectorSession session)
        {
            lock (_gate)
            {
                _componentCollectors.Remove(session);
            }
        }

        private void CancelAllCollectors()
        {
            MessageCollectorSession[] messageSessions;
            ComponentCollectorSession[] componentSessions;
            lock (_gate)
            {
                messageSessions = _messageCollectors.ToArray();
                componentSessions = _componentCollectors.ToArray();
                _messageCollectors.Clear();
                _componentCollectors.Clear();
            }

            for (var i = 0; i < messageSessions.Length; i++)
            {
                messageSessions[i].Cancel();
            }

            for (var i = 0; i < componentSessions.Length; i++)
            {
                componentSessions[i].Cancel();
            }
        }
    }
}
