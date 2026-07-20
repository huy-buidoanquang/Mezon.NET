using System.Collections.Generic;

namespace Mezon.Net.Client
{
    /// <summary>Select / dropdown control (<c>SelectComponent</c> / <c>IMessageSelect</c>).</summary>
    public sealed class SelectMessageComponent : MessageComponent
    {
        public SelectMessageComponent(
            string id,
            IReadOnlyList<MessageSelectOption> options,
            MessageSelectType selectType = MessageSelectType.Text,
            string? placeholder = null,
            int? minOptions = null,
            int? maxOptions = null,
            bool disabled = false,
            MessageSelectOption? valueSelected = null)
            : base(id, MessageComponentType.Select)
        {
            Options = options;
            SelectType = selectType;
            Placeholder = placeholder;
            MinOptions = minOptions;
            MaxOptions = maxOptions;
            Disabled = disabled;
            ValueSelected = valueSelected;
        }

        public MessageSelectType SelectType { get; }
        public IReadOnlyList<MessageSelectOption> Options { get; }
        public string? Placeholder { get; }
        public int? MinOptions { get; }
        public int? MaxOptions { get; }
        public bool Disabled { get; }
        public MessageSelectOption? ValueSelected { get; }
    }
}
