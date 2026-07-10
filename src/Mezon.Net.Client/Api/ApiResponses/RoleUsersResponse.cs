using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class RoleUsersResponse
    {
        [JsonProperty("cursor")]
        public string? Cursor { get; set; }

        [JsonProperty("role_users")]
        public List<RoleUserResponse>? RoleUsers { get; set; }
    }
}
