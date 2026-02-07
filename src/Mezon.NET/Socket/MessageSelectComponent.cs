using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// A Select Menu component.
    /// </summary>
    public class MessageSelectComponent : MessageComponent
    {
        public override MessageComponentTypeEnum Type => MessageComponentTypeEnum.SELECT;

        [JsonPropertyName("component")]
        public MessageSelect Component { get; set; }
    }

    /// <summary>
    /// Contains the specific properties for a select menu component.
    /// </summary>
    public class MessageSelect
    {
        [JsonPropertyName("style")]
        public MessageSelectTypeEnum? Style { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageSelectTypeEnum
    {
        TEXT = 1,
        USER = 2,
        ROLE = 3,
        CHANNEL = 4,
    }
}
