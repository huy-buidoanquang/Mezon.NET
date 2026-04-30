using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    public class PermissionsResponse
    {
        [JsonProperty("max_level_permission")]
        public int? MaxLevelPermission { get; set; }

        [JsonProperty("permissions")]
        public List<PermissionResponse>? Permissions { get; set; }
    }
}
