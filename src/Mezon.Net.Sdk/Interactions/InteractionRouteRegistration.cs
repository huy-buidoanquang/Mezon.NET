using System;

namespace Mezon.Net.Sdk.Interactions
{
    public sealed class InteractionRouteRegistration
    {
        private readonly InteractionRouter _router;
        private readonly InteractionRoute _route;

        internal InteractionRouteRegistration(InteractionRouter router, InteractionRoute route)
        {
            _router = router;
            _route = route;
        }

        public InteractionRouteRegistration WithOwner(long userId)
        {
            _route.OwnerUserId = userId;
            return this;
        }

        public InteractionRouteRegistration OneShot()
        {
            _route.OneShot = true;
            return this;
        }

        public InteractionRouteRegistration ExpiresAt(DateTimeOffset expiresAt)
        {
            _route.ExpiresAt = expiresAt;
            return this;
        }

        public InteractionRouteRegistration ExpiresAfter(TimeSpan duration)
            => ExpiresAt(DateTimeOffset.UtcNow.Add(duration));
    }
}
