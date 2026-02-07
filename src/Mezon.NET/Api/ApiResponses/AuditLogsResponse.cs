using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class AuditLogsResponse
    {
        [JsonPropertyName("date_log")]
        public string DateLog { get; set; }

        [JsonPropertyName("logs")]
        public List<AuditLogResponse>? Logs { get; set; }

        [JsonPropertyName("total_count")]
        public int? TotalCount { get; set; }
    }
}
