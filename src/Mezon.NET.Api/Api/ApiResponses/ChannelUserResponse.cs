using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class ChannelUserResponse
    {
        [JsonProperty("clan_avatar")]
        public string? ClanAvatar { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("clan_nick")]
        public string? ClanNick { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("role_id")]
        public List<string>? RoleId { get; set; }

        [JsonProperty("thread_id")]
        public string? ThreadId { get; set; }

        [JsonProperty("user_id")]
        public string? UserId { get; set; }
    }
}
