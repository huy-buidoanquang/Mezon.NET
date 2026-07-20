using System.Collections.Generic;

namespace Mezon.Net.Client
{
    /// <summary>
    /// One action row in content <c>components</c> — a list of <see cref="MessageComponent"/>
    /// (<c>IMessageActionRow</c>).
    /// </summary>
    public sealed class MessageActionRow
    {
        public MessageActionRow(IReadOnlyList<MessageComponent> components)
        {
            Components = components;
        }

        public IReadOnlyList<MessageComponent> Components { get; }
    }
}
