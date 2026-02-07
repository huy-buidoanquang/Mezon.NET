using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class RoleUsersResponse
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }

        [JsonPropertyName("role_users")]
        public List<RoleUserResponse>? RoleUsers { get; set; }
    }
}
