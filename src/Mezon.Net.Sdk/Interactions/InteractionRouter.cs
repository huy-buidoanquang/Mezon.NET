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

        /// <summary>
        /// Routes a button click. The server forwards the clicking user's id exactly as the client sent it, so the
        /// actor is <see cref="InteractionActorTrust.ClientSupplied"/> and routes that require a server-authenticated
        /// actor reject button clicks.
        /// </summary>
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
                data.ExtraData,
                InteractionActorTrust.ClientSupplied);
            return await HandleAsync(client, interaction, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Routes a dropdown selection. The server replaces the user id with the authenticated session user, so the
        /// actor is <see cref="InteractionActorTrust.ServerAuthenticated"/>.
        /// </summary>
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
                values,
                InteractionActorTrust.ServerAuthenticated);
            return await HandleAsync(client, interaction, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<InteractionExecutionResult> HandleAsync(
            MezonClient client,
            IInteraction interaction,
            CancellationToken cancellationToken)
        {
            var claim = ClaimRoute(interaction, out var route);
            if (claim == InteractionExecutionResult.NotHandled)
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

            if (route is null)
            {
                return claim;
            }

            return await InvokeHandlerAsync(client, interaction, route.Handler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Finds, checks and (for one-shot routes) removes the matching route under one lock, so concurrent clicks can
        /// never run a one-shot handler twice. Returns <see cref="InteractionExecutionResult.Handled"/> with
        /// <paramref name="claimed"/> set when the handler should run.
        /// </summary>
        private InteractionExecutionResult ClaimRoute(IInteraction interaction, out InteractionRoute? claimed)
        {
            claimed = null;
            lock (_routeGate)
            {
                var now = Time.GetUtcNow();
                var route = FindRouteLocked(interaction);
                for (var i = _routes.Count - 1; i >= 0; i--)
                {
                    if (_routes[i].IsExpired(now))
                    {
                        _routes.RemoveAt(i);
                    }
                }

                if (route is null)
                {
                    return InteractionExecutionResult.NotHandled;
                }

                if (route.IsExpired(now))
                {
                    return InteractionExecutionResult.Expired;
                }

                if (!route.CanBeTriggeredBy(interaction.UserId))
                {
                    return InteractionExecutionResult.Unauthorized;
                }

                if (route.RequiresServerAuthenticatedActor
                    && (interaction is not IInteractionActor actor
                        || actor.ActorTrust != InteractionActorTrust.ServerAuthenticated))
                {
                    return InteractionExecutionResult.Unauthorized;
                }

                if (route.OneShot)
                {
                    _routes.Remove(route);
                }

                claimed = route;
                return InteractionExecutionResult.Handled;
            }
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

        private InteractionRoute? FindRouteLocked(IInteraction interaction)
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

        private async Task<InteractionExecutionResult> InvokeHandlerAsync(
            MezonClient client,
            IInteraction interaction,
            InteractionHandler handler,
            CancellationToken cancellationToken)
        {
            try
            {
                if (interaction.ChannelId <= 0)
                {
                    await client._logger.WarningAsync(
                        $"Interaction '{interaction.CustomId}' has no channel id; handler skipped.").ConfigureAwait(false);
                    return InteractionExecutionResult.Failed;
                }

                // Cached channels resolve synchronously; otherwise fetch the real channel so replies carry the right
                // clan and stream mode instead of a clan-0 placeholder.
                var channel = await client.GetChannelAsync(interaction.ChannelId, cancellationToken).ConfigureAwait(false);
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
            catch (Exception ex)
            {
                await client._logger.WarningAsync(
                    $"Interaction handler for '{interaction.CustomId}' failed.", ex).ConfigureAwait(false);
                return InteractionExecutionResult.Failed;
            }
        }
    }
}
