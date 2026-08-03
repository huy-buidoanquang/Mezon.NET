namespace Mezon.Net.Client
{
    /// <summary>
    /// Base type for interactive controls in a <see cref="MessageActionRow"/>
    /// (<c>IMessageComponent</c>).
    /// </summary>
    public abstract class MessageComponent
    {
        protected MessageComponent(string id, MessageComponentType type)
        {
            Id = id;
            ComponentType = type;
        }

        /// <summary>Stable control id used by button/select click events.</summary>
        public string Id { get; }

        /// <summary>Wire <c>type</c> discriminant (<see cref="MessageComponentType"/>).</summary>
        public MessageComponentType ComponentType { get; }
    }
}
