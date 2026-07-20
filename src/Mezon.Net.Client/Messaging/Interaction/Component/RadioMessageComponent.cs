using System.Collections.Generic;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Radio component — wire <c>component</c> is the options array; <see cref="MaxOptions"/>
    /// is a sibling on the component envelope (<c>IMessageComponent.max_options</c>).
    /// </summary>
    public sealed class RadioMessageComponent : MessageComponent
    {
        public RadioMessageComponent(
            string id,
            IReadOnlyList<MessageRadioOption> options,
            int? maxOptions = null)
            : base(id, MessageComponentType.Radio)
        {
            Options = options;
            MaxOptions = maxOptions;
        }

        public IReadOnlyList<MessageRadioOption> Options { get; }
        public int? MaxOptions { get; }
    }
}
