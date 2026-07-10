using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Client
{
    public class CreateChannelDescriptionRequest
    {
        [JsonProperty("app_id")]
        public string? AppId { get; set; }

        [JsonProperty("category_id")]
        public string? CategoryId { get; set; }

        [JsonProperty("channel_id")]
        public string? ChannelId { get; set; }

        [JsonProperty("channel_label")]
        public string? ChannelLabel { get; set; }

        [JsonProperty("channel_private")]
        public int? ChannelPrivate { get; set; }

        [JsonProperty("clan_id")]
        public string? ClanId { get; set; }

        [JsonProperty("parent_id")]
        public string? ParentId { get; set; }

        [JsonProperty("type")]
        public int? Type { get; set; }

        [JsonProperty("user_ids")]
        public List<string>? UserIds { get; set; }
    }
}
