using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a custom status for a user in a clan.
    /// </summary>
    public class CustomStatusSend : SocketSendBase
    {
        /// <summary>
        /// The ID of the clan.
        /// </summary>
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        /// <summary>
        /// The ID of the user.
        /// </summary>
        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        /// <summary>
        /// The username of the user.
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; }

        /// <summary>
        /// The new status message.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
}
