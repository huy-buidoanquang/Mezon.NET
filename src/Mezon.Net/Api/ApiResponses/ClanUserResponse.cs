using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ClanUserResponse
    {
        [JsonPropertyName("clan_avatar")]
        public string ClanAvatar { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_nick")]
        public string ClanNick { get; set; }

        [JsonPropertyName("role_id")]
        public List<string>? RoleId { get; set; }

        [JsonPropertyName("user")]
        public ApiUser? User { get; set; }
    }
}
