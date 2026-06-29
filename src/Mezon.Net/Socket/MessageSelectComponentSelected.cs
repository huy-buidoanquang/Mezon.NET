using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents an event that is fired when a user makes a selection in a dropdown box (select menu).
    /// </summary>
    public class MessageSelectComponentSelected : SocketSendBase
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("selectbox_id")]
        public string SelectboxId { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("value")]
        public List<string> Value { get; set; }
    }
}
