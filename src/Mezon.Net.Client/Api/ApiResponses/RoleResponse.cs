using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class RoleResponse
    {
        [JsonProperty("active")]
        public int? Active { get; set; }

        [JsonProperty("allow_mention")]
        public int? AllowMention { get; set; }

        [JsonProperty("channel_ids")]
        public List<string>? ChannelIds { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("color")]
        public string? Color { get; set; }

        [JsonProperty("creator_id")]
        public string? CreatorId { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("display_online")]
        public int? DisplayOnline { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("max_level_permission")]
        public int? MaxLevelPermission { get; set; }

        [JsonProperty("permission_list")]
        public PermissionsResponse? PermissionList { get; set; }

        [JsonProperty("role_channel_active")]
        public int? RoleChannelActive { get; set; }

        [JsonProperty("role_icon")]
        public string? RoleIcon { get; set; }

        [JsonProperty("role_user_list")]
        public RoleUsersResponse? RoleUserList { get; set; }

        [JsonProperty("slug")]
        public string? Slug { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("order_role")]
        public int? OrderRole { get; set; }
    }
}
