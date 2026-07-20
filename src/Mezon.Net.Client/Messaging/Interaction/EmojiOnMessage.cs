namespace Mezon.Net.Client
{
    /// <summary>Custom emoji token (<c>ej</c>) — UTF-16 start/end into <c>t</c>.</summary>
    public readonly record struct EmojiOnMessage(string? EmojiId, int? Start, int? End);
}
