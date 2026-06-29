using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiRequests
{
    public class UpdateRoleRequest
    {
        [JsonPropertyName("active_permission_ids")]
        public List<string>? ActivePermissionIds { get; set; }

        [JsonPropertyName("add_user_ids")]
        public List<string>? AddUserIds { get; set; }

        [JsonPropertyName("allow_mention")]
        public int? AllowMention { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("display_online")]
        public int? DisplayOnline { get; set; }

        [JsonPropertyName("max_permission_id")]
        public string MaxPermissionId { get; set; } = "";

        [JsonPropertyName("remove_permission_ids")]
        public List<string>? RemovePermissionIds { get; set; }

        [JsonPropertyName("remove_user_ids")]
        public List<string>? RemoveUserIds { get; set; }

        [JsonPropertyName("role_icon")]
        public string RoleIcon { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }
}
