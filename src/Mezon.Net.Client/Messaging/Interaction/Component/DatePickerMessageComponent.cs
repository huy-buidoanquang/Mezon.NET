namespace Mezon.Net.Client
{
    public sealed class DatePickerMessageComponent : MessageComponent
    {
        public DatePickerMessageComponent(string id, string? value = null)
            : base(id, MessageComponentType.DatePicker)
        {
            Value = value;
        }

        /// <summary>ISO / wire string value when present.</summary>
        public string? Value { get; }
    }
}
