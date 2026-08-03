namespace Mezon.Net.Client
{
    /// <summary>
    /// Button control (<c>ButtonComponent</c> / <c>IButtonMessage</c>).
    /// </summary>
    public sealed class ButtonMessageComponent : MessageComponent
    {
        public ButtonMessageComponent(
            string id,
            string label,
            int style = (int)MessageButtonStyle.Primary,
            bool disable = false,
            string? url = null,
            string? icon = null)
            : base(id, MessageComponentType.Button)
        {
            Label = label;
            Style = style;
            Disable = disable;
            Url = url;
            Icon = icon;
        }

        public string Label { get; }

        /// <summary><see cref="MessageButtonStyle"/> as int for wire parity.</summary>
        public int Style { get; }

        public bool Disable { get; }
        public string? Url { get; }
        public string? Icon { get; }
    }
}
