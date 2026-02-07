using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    public class ChannelAppResponse
    {
        [JsonPropertyName("app_id")]
        public string AppId { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }
    }
}
