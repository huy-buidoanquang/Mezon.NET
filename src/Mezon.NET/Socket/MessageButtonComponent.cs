using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// A Button component.
    /// </summary>
    public class MessageButtonComponent : MessageComponent
    {
        public override MessageComponentTypeEnum Type => MessageComponentTypeEnum.BUTTON;

        [JsonPropertyName("component")]
        public ButtonMessage Component { get; set; }
    }

    /// <summary>
    /// Contains the specific properties for a button component.
    /// </summary>
    public class ButtonMessage
    {
        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("disable")]
        public bool? Disable { get; set; }

        [JsonPropertyName("style")]
        public ButtonMessageStyleEnum? Style { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ButtonMessageStyleEnum
    {
        PRIMARY = 1,
        SECONDARY = 2,
        SUCCESS = 3,
        DANGER = 4,
        LINK = 5,
    }
}
