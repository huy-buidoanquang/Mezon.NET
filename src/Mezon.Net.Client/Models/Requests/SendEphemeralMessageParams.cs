using System.Collections.Generic;

namespace Mezon.Net.Models
{
    /// <summary>
    /// Input for sending an ephemeral message via realtime envelope.
    /// </summary>
    public readonly struct SendEphemeralMessageParams
    {
        public IReadOnlyList<long> ReceiverIds { get; }
        public SendChannelMessageParams Message { get; }
        public long? Id { get; }

        public SendEphemeralMessageParams(
            IReadOnlyList<long> receiverIds,
            SendChannelMessageParams message,
            long? id = null)
        {
            ReceiverIds = receiverIds;
            Message = message;
            Id = id;
        }
    }
}
