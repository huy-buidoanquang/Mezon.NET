using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class AuditLogsResponse
    {
        [JsonProperty("date_log")]
        public string? DateLog { get; set; }

        [JsonProperty("logs")]
        public List<AuditLogResponse>? Logs { get; set; }

        [JsonProperty("total_count")]
        public int? TotalCount { get; set; }
    }
}
