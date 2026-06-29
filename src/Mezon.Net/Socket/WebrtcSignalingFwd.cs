using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a WebRTC signaling forward message.
    /// </summary>
    public class WebrtcSignalingFwd : SocketSendBase
    {
        [JsonPropertyName("receiver_id")]
        public string ReceiverId { get; set; }

        [JsonPropertyName("data_type")]
        public int DataType { get; set; }

        [JsonPropertyName("json_data")]
        public string JsonData { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("caller_id")]
        public string CallerId { get; set; }
    }
}
