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

        /// <summary>
        /// Only lets <paramref name="userId"/> trigger this route. For button routes the user id is client-supplied, so
        /// this filters accidental clicks but is not a security boundary.
        /// </summary>
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

        /// <summary>
        /// Rejects interactions whose actor is not <see cref="InteractionActorTrust.ServerAuthenticated"/>. Button
        /// clicks are <see cref="InteractionActorTrust.ClientSupplied"/>, so a button route with this requirement
        /// always returns <see cref="InteractionExecutionResult.Unauthorized"/>.
        /// </summary>
        public InteractionRouteRegistration RequireServerAuthenticatedActor()
        {
            _route.RequiresServerAuthenticatedActor = true;
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
