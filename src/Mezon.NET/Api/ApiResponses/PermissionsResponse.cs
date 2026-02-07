using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class PermissionsResponse
    {
        [JsonPropertyName("max_level_permission")]
        public int? MaxLevelPermission { get; set; }

        [JsonPropertyName("permissions")]
        public List<PermissionResponse>? Permissions { get; set; }
    }
}
