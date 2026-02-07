using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class ClanUserResponse
    {
        [JsonProperty("clan_avatar")]
        public string? ClanAvatar { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("clan_nick")]
        public string? ClanNick { get; set; }

        [JsonProperty("role_id")]
        public List<string>? RoleId { get; set; }

        [JsonProperty("user")]
        public UserResponse? User { get; set; }
    }
}
