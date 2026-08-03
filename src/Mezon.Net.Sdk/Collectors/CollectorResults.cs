using System.Collections.Generic;
using Mezon.Net.Sdk.Entities;
using Mezon.Net.Sdk.Interactions;

namespace Mezon.Net.Sdk.Collectors
{
    public sealed class MessageCollectorResult
    {
        public MessageCollectorResult(CollectorStatus status, Message? message = null, IReadOnlyList<Message>? messages = null)
        {
            Status = status;
            Message = message;
            Messages = messages ?? System.Array.Empty<Message>();
        }

        public CollectorStatus Status { get; }
        public Message? Message { get; }
        public IReadOnlyList<Message> Messages { get; }
    }

    public sealed class ComponentCollectorResult
    {
        public ComponentCollectorResult(CollectorStatus status, IInteraction? interaction = null, IReadOnlyList<IInteraction>? interactions = null)
        {
            Status = status;
            Interaction = interaction;
            Interactions = interactions ?? System.Array.Empty<IInteraction>();
        }

        public CollectorStatus Status { get; }
        public IInteraction? Interaction { get; }
        public IReadOnlyList<IInteraction> Interactions { get; }
    }
}
