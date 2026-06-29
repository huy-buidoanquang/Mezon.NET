using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents an incoming call push notification.
    /// </summary>
    public class IncomingCallPush : SocketSendBase
    {
        [JsonPropertyName("receiver_id")]
        public string ReceiverId { get; set; }

        [JsonPropertyName("json_data")]
        public string JsonData { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("caller_id")]
        public string CallerId { get; set; }
    }
}
