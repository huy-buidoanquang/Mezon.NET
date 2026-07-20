namespace Mezon.Net.Client
{
    public sealed class InputMessageComponent : MessageComponent
    {
        public InputMessageComponent(
            string id,
            string? placeholder = null,
            string? inputType = null,
            string? defaultValue = null,
            bool textarea = false,
            bool required = false,
            bool disabled = false,
            int? style = null,
            string? nestedComponentId = null)
            : base(id, MessageComponentType.Input)
        {
            Placeholder = placeholder;
            InputType = inputType;
            DefaultValue = defaultValue;
            Textarea = textarea;
            Required = required;
            Disabled = disabled;
            Style = style;
            NestedComponentId = nestedComponentId;
        }

        public string? Placeholder { get; }

        /// <summary>HTML-like input type (<c>text</c>, …).</summary>
        public string? InputType { get; }

        public string? DefaultValue { get; }
        public bool Textarea { get; }
        public bool Required { get; }
        public bool Disabled { get; }
        public int? Style { get; }

        /// <summary>
        /// Optional nested <c>component.id</c> used by mezon-sdk InteractiveBuilder
        /// (<c>{id}-component</c>).
        /// </summary>
        public string? NestedComponentId { get; }
    }
}
