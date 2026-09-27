namespace Mezon.Net.Client;

public sealed class MessageEmbedAuthor
{
    public MessageEmbedAuthor(string name, string? iconUrl = null, string? url = null)
    {
        Name = name;
        IconUrl = iconUrl;
        Url = url;
    }

    public string Name { get; }
    public string? IconUrl { get; }
    public string? Url { get; }
}
