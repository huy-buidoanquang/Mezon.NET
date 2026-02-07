using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ChannelUserResponse
    {
        [JsonPropertyName("clan_avatar")]
        public string ClanAvatar { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_nick")]
        public string ClanNick { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("role_id")]
        public List<string>? RoleId { get; set; }

        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }
}
