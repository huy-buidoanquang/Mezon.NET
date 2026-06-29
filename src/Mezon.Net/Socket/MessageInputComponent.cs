using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// A Text Input component.
    /// </summary>
    public class MessageInputComponent : MessageComponent
    {
        public override MessageComponentTypeEnum Type => MessageComponentTypeEnum.INPUT;

        [JsonPropertyName("component")]
        public MessageInput Component { get; set; }
    }

    /// <summary>
    /// Contains the specific properties for a text input component.
    /// </summary>
    public class MessageInput { /* some input specific properties */ }
}
