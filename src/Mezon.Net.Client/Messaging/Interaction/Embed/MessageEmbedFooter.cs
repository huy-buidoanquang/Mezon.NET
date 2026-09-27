namespace Mezon.Net.Client;

public sealed class MessageEmbedFooter
{
    public MessageEmbedFooter(string text, string? iconUrl = null)
    {
        Text = text;
        IconUrl = iconUrl;
    }

    public string Text { get; }
    public string? IconUrl { get; }
}
