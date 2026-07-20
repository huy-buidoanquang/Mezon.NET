namespace Mezon.Net.Client
{
    /// <summary>Radio option (<c>IMessageRatioOption</c> / <c>RadioFieldOption</c>).</summary>
    public readonly record struct MessageRadioOption(
        string Label,
        string Value,
        string? Name = null,
        string? Description = null,
        int? Style = null,
        bool Disabled = false);
}
