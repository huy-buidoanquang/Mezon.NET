using System.Text.Json;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Fallback for unrecognized or partially shaped component payloads.
    /// Preserves the raw <c>component</c> JSON for round-trip.
    /// </summary>
    public sealed class UnknownMessageComponent : MessageComponent
    {
        public UnknownMessageComponent(string id, int type, JsonElement componentPayload)
            : base(id, (MessageComponentType)type)
        {
            RawType = type;
            ComponentPayload = componentPayload;
        }

        public int RawType { get; }
        public JsonElement ComponentPayload { get; }
    }
}
