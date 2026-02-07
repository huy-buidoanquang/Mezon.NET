using System.Collections.Generic;
using System.Text.Json.Serialization;
using Mezon.NET.Api;

namespace Mezon.NET.Socket
{
    // C# equivalent classes for all the interfaces defined in socket.ts
    // e.g., Presence, Channel, ChannelMessage, etc.

    public class Presence
    {
        [JsonPropertyName("user_id")] public string UserId { get; set; }
        [JsonPropertyName("session_id")] public string SessionId { get; set; }
        [JsonPropertyName("username")] public string Username { get; set; }
        [JsonPropertyName("node")] public string Node { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; }
        [JsonPropertyName("is_mobile")] public bool IsMobile { get; set; }
        [JsonPropertyName("metadata")] public string Metadata { get; set; }
    }

    public class NotificationInfo
    {
        public int? Code { get; set; }
        public object Content { get; set; }
        public string CreateTime { get; set; }
        public string Id { get; set; }
        public bool? Persistent { get; set; }
        public string SenderId { get; set; }
        public string Subject { get; set; }
        public string ChannelId { get; set; }
        public string ClanId { get; set; }
        public ApiChannelDescription Channel { get; set; }
        public string TopicId { get; set; }
    }

    public class Channel
    {
        public string Id { get; set; }
        public string ChanelLabel { get; set; }
        public List<Presence> Presences { get; set; }
        public Presence Self { get; set; }
        public string ClanLogo { get; set; }
        public string CategoryName { get; set; }
    }

    public class ChannelPresenceEvent
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }
        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }
        [JsonPropertyName("mode")]
        public int Mode { get; set; }
        [JsonPropertyName("joins")]
        public List<Presence> Joins { get; set; }
        [JsonPropertyName("leaves")]
        public List<Presence> Leaves { get; set; }
    }

    public class ChannelMessageAck
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }
        [JsonPropertyName("mode")]
        public int Mode { get; set; }
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("username")]
        public string Username { get; set; }
        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }
        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }
        [JsonPropertyName("persistence")]
        public bool Persistence { get; set; }
    }

    public class SocketError
    {
        public int Code { get; set; }
        public string Message { get; set; }
    }
}
