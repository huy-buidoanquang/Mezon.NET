using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class UpdateRoleRequest
    {
        [JsonProperty("active_permission_ids")]
        public List<string>? ActivePermissionIds { get; set; }

        [JsonProperty("add_user_ids")]
        public List<string>? AddUserIds { get; set; }

        [JsonProperty("allow_mention")]
        public int? AllowMention { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("color")]
        public string? Color { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("display_online")]
        public int? DisplayOnline { get; set; }

        [JsonProperty("max_permission_id")]
        public string? MaxPermissionId { get; set; } = "";

        [JsonProperty("remove_permission_ids")]
        public List<string>? RemovePermissionIds { get; set; }

        [JsonProperty("remove_user_ids")]
        public List<string>? RemoveUserIds { get; set; }

        [JsonProperty("role_icon")]
        public string? RoleIcon { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }
    }
}
