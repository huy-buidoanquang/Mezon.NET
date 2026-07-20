namespace Mezon.Net.Client
{
    /// <summary>Select option (<c>IMessageSelectOption</c> / <c>SelectFieldOption</c>).</summary>
    public readonly record struct MessageSelectOption(
        string Label,
        string Value,
        string? Description = null,
        bool Default = false);
}
