using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class RoleResponse
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("allow_mention")]
        public int? AllowMention { get; set; }

        [JsonPropertyName("channel_ids")]
        public List<string>? ChannelIds { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("display_online")]
        public int? DisplayOnline { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("max_level_permission")]
        public int? MaxLevelPermission { get; set; }

        [JsonPropertyName("permission_list")]
        public ApiPermissionList? PermissionList { get; set; }

        [JsonPropertyName("role_channel_active")]
        public int? RoleChannelActive { get; set; }

        [JsonPropertyName("role_icon")]
        public string RoleIcon { get; set; }

        [JsonPropertyName("role_user_list")]
        public ApiRoleUserList? RoleUserList { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("order_role")]
        public int? OrderRole { get; set; }
    }
}
