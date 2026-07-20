using System;

namespace Mezon.Net.Sdk.Interactions
{
    internal sealed class InteractionRoute
    {
        internal InteractionRoute(
            string customId,
            InteractionRouteMatchKind matchKind,
            InteractionKind kind,
            InteractionHandler handler)
        {
            CustomId = customId ?? throw new ArgumentNullException(nameof(customId));
            MatchKind = matchKind;
            Kind = kind;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public string CustomId { get; }
        public InteractionRouteMatchKind MatchKind { get; }
        public InteractionKind Kind { get; }
        public InteractionHandler Handler { get; }
        public long? OwnerUserId { get; set; }
        public bool OneShot { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }

        public bool IsExpired(DateTimeOffset now)
            => ExpiresAt.HasValue && now >= ExpiresAt.Value;

        public bool CanBeTriggeredBy(long userId)
            => !OwnerUserId.HasValue || OwnerUserId.Value == userId;

        public bool Matches(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
            {
                return false;
            }

            return MatchKind switch
            {
                InteractionRouteMatchKind.Exact => string.Equals(CustomId, componentId, StringComparison.Ordinal),
                InteractionRouteMatchKind.Prefix => componentId.StartsWith(CustomId, StringComparison.Ordinal),
                _ => false,
            };
        }
    }
}
