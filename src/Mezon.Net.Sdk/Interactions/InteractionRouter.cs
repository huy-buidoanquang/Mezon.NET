using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Entities;

namespace Mezon.Net.Sdk.Interactions
{
    public sealed class InteractionRouter
    {
        private readonly List<InteractionRoute> _routes = new List<InteractionRoute>();
        private readonly object _routeGate = new object();
        private MezonClient? _client;
        private InteractionHandler? _unknownHandler;
        internal TimeProvider Time { get; set; } = TimeProvider.System;

        public InteractionRouteRegistration OnButton(string customId, InteractionHandler handler)
            => RegisterRoute(customId, InteractionKind.Button, handler);

        public InteractionRouteRegistration OnSelect(string customId, InteractionHandler handler)
            => RegisterRoute(customId, InteractionKind.Select, handler);

        public InteractionRouter OnUnknown(InteractionHandler handler)
        {
            _unknownHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public MezonClient Attach(MezonClient client)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (_client is not null)
            {
                throw new InvalidOperationException("InteractionRouter is already attached to a client.");
            }

            _client = client;
            ClientInteractionHub.GetOrCreate(client).RegisterRouter(this);
            return client;
        }

        public void Detach()
        {
            if (_client is null)
            {
                return;
            }

            ClientInteractionHub.GetOrCreate(_client).UnregisterRouter(this);
            _client = null;
        }

        public async Task<InteractionExecutionResult> HandleButtonAsync(
            MezonClient client,
            MessageButtonClickedEventData eventData,
            CancellationToken cancellationToken = default)
        {
            var data = (MessageButtonClickedResponse)eventData;
            var interaction = new ButtonInteraction(
                data.MessageId,
                data.ChannelId,
                data.ButtonId,
                data.UserId,
                data.SenderId,
                data.ExtraData);
            return await HandleAsync(client, interaction, cancellationToken).ConfigureAwait(false);
        }

        public async Task<InteractionExecutionResult> HandleSelectAsync(
            MezonClient client,
            DropdownBoxSelectedEventData eventData,
            CancellationToken cancellationToken = default)
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
            return await HandleAsync(client, interaction, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<InteractionExecutionResult> HandleAsync(
            MezonClient client,
            IInteraction interaction,
            CancellationToken cancellationToken)
        {
            var route = FindRoute(interaction);
            if (route is null)
            {
                if (_unknownHandler is null)
                {
                    return InteractionExecutionResult.NotHandled;
                }

                return await InvokeHandlerAsync(
                    client,
                    new UnknownInteraction(interaction),
                    _unknownHandler,
                    cancellationToken).ConfigureAwait(false);
            }

            if (route.IsExpired(Time.GetUtcNow()))
            {
                return InteractionExecutionResult.Expired;
            }

            if (!route.CanBeTriggeredBy(interaction.UserId))
            {
                return InteractionExecutionResult.Unauthorized;
            }

            if (route.OneShot)
            {
                RemoveRoute(route);
            }

            return await InvokeHandlerAsync(client, interaction, route.Handler, cancellationToken).ConfigureAwait(false);
        }

        private InteractionRouteRegistration RegisterRoute(string customId, InteractionKind kind, InteractionHandler handler)
        {
            if (string.IsNullOrWhiteSpace(customId))
            {
                throw new ArgumentException("Custom id is required.", nameof(customId));
            }

            var matchKind = InteractionRouteMatchKind.Exact;
            var routeId = customId.Trim();
            if (routeId.EndsWith('*'))
            {
                matchKind = InteractionRouteMatchKind.Prefix;
                routeId = routeId[..^1];
            }

            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentException("Custom id is required.", nameof(customId));
            }

            var route = new InteractionRoute(routeId, matchKind, kind, handler);
            lock (_routeGate)
            {
                _routes.Add(route);
            }

            return new InteractionRouteRegistration(this, route);
        }

        private InteractionRoute? FindRoute(IInteraction interaction)
        {
            lock (_routeGate)
            {
                InteractionRoute? bestPrefix = null;
                for (var i = 0; i < _routes.Count; i++)
                {
                    var route = _routes[i];
                    if (route.Kind != interaction.Kind && route.Kind != InteractionKind.Unknown)
                    {
                        continue;
                    }

                    if (route.MatchKind == InteractionRouteMatchKind.Exact && route.Matches(interaction.CustomId))
                    {
                        return route;
                    }

                    if (route.MatchKind == InteractionRouteMatchKind.Prefix && route.Matches(interaction.CustomId))
                    {
                        if (bestPrefix is null || route.CustomId.Length > bestPrefix.CustomId.Length)
                        {
                            bestPrefix = route;
                        }
                    }
                }

                return bestPrefix;
            }
        }

        private void RemoveRoute(InteractionRoute route)
        {
            lock (_routeGate)
            {
                _routes.Remove(route);
            }
        }

        private async Task<InteractionExecutionResult> InvokeHandlerAsync(
            MezonClient client,
            IInteraction interaction,
            InteractionHandler handler,
            CancellationToken cancellationToken)
        {
            try
            {
                var channel = client.GetOrCreateChannelStub(interaction.ChannelId);
                Message? message = null;
                if (interaction.MessageId != 0 && channel.Messages.TryGet(interaction.MessageId, out var cached))
                {
                    message = cached;
                }

                var user = await client.GetUserAsync(interaction.UserId, cancellationToken).ConfigureAwait(false);
                var context = new InteractionContext(client, interaction, channel, message, user, cancellationToken);
                await handler(context).ConfigureAwait(false);
                return InteractionExecutionResult.Handled;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return InteractionExecutionResult.Failed;
            }
        }
    }
}
