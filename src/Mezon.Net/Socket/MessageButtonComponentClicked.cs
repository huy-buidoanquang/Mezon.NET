using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents an event that is fired when a user clicks a message button.
    /// </summary>
    public class MessageButtonComponentClicked : SocketSendBase
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("button_id")]
        public string ButtonId { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("extra_data")]
        public string ExtraData { get; set; }
    }
}
