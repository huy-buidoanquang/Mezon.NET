using Newtonsoft.Json;

namespace Mezon.NET.Api
{
    public class ChannelAppResponse
    {
        [JsonProperty("app_id")]
        public string? AppId { get; set; }

        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("app_url")]
        public string? AppUrl { get; set; }
    }
}
