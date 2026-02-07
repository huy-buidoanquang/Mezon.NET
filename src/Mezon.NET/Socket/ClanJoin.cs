using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents the payload for joining a realtime chat clan.
    /// </summary>
    public class ClanJoin : SocketSendBase
    {
        [JsonPropertyName("clan_join")]
        public ClanJoinDetails ClanJoinDetails { get; set; }
    }

    /// <summary>
    /// Contains the specific details for the clan to be joined.
    /// </summary>
    public class ClanJoinDetails
    {
        /// <summary>
        /// The unique identifier of the clan to join.
        /// </summary>
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }
}
