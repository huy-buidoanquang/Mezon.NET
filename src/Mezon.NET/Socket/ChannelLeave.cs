using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    // <summary>
    /// Represents the payload for leaving a realtime chat channel.
    /// </summary>
    public class ChannelLeave : SocketSendBase
    {
        [JsonPropertyName("channel_leave")]
        public ChannelLeaveDetails ChannelLeaveDetails { get; set; }
    }

    /// <summary>
    /// Contains the specific details of the channel to be left.
    /// </summary>
    public class ChannelLeaveDetails
    {
        /// <summary>
        /// The ID of the channel to leave.
        /// </summary>
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// The message mode.
        /// </summary>
        [JsonPropertyName("mode")]
        public int Mode { get; set; }

        /// <summary>
        /// The channel label.
        /// </summary>
        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        /// <summary>
        /// Indicates if the channel is public.
        /// </summary>
        [JsonPropertyName("is_public")]
        public bool IsPublic { get; set; }
    }
}
