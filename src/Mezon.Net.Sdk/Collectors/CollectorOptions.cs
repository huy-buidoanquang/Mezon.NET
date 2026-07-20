using System;
using System.Collections.Generic;
using Mezon.Net.Sdk.Entities;
using Mezon.Net.Sdk.Interactions;

namespace Mezon.Net.Sdk.Collectors
{
    public sealed class MessageCollectorOptions
    {
        public Func<Message, bool>? Filter { get; init; }
        public long? UserId { get; init; }
        public long? ChannelId { get; init; }
        public long? MessageId { get; init; }
        public TimeSpan? Timeout { get; init; }
        public TimeSpan? IdleTimeout { get; init; }
        public int Max { get; init; } = 1;
    }

    public sealed class ComponentCollectorOptions
    {
        public Func<IInteraction, bool>? Filter { get; init; }
        public long? UserId { get; init; }
        public long? ChannelId { get; init; }
        public long? MessageId { get; init; }
        public string? ComponentId { get; init; }
        public TimeSpan? Timeout { get; init; }
        public TimeSpan? IdleTimeout { get; init; }
        public int Max { get; init; } = 1;
    }
}
