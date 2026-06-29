using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// A base class for message components, enabling polymorphism for deserialization.
    /// The 'type' property in the JSON will determine which derived class is instantiated.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(MessageButtonComponent), nameof(MessageComponentTypeEnum.BUTTON))]
    [JsonDerivedType(typeof(MessageSelectComponent), nameof(MessageComponentTypeEnum.SELECT))]
    [JsonDerivedType(typeof(MessageInputComponent), nameof(MessageComponentTypeEnum.INPUT))]
    public abstract class MessageComponent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        public abstract MessageComponentTypeEnum Type { get; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageComponentTypeEnum
    {
        BUTTON = 1,
        SELECT = 2,
        INPUT = 3,
        DATEPICKER = 4,
        RADIO = 5,
        ANIMATION = 6,
    }
}
