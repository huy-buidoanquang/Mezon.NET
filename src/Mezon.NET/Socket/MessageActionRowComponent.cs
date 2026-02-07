using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a row of interactive components in a message.
    /// </summary>
    public class MessageActionRowComponent
    {
        [JsonPropertyName("components")]
        public List<MessageComponent> Components { get; set; }
    }
}
