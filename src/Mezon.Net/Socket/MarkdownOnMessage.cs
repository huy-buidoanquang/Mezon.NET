using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a markdown entity within a message.
    /// </summary>
    public class MarkdownOnMessage : StartEndIndex
    {
        [JsonPropertyName("type")]
        public MarkdownTypeEnum? Type { get; set; }
    }

    /// Defines the types of markdown formatting.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MarkdownTypeEnum
    {
        [EnumMember(Value = "t")]
        TRIPLE,

        [EnumMember(Value = "s")]
        SINGLE,

        [EnumMember(Value = "pre")]
        PRE,

        [EnumMember(Value = "c")]
        CODE,

        [EnumMember(Value = "b")]
        BOLD,

        [EnumMember(Value = "lk")]
        LINK,

        [EnumMember(Value = "vk")]
        VOICE_LINK,

        [EnumMember(Value = "lk_yt")]
        LINKYOUTUBE,
    }
}
