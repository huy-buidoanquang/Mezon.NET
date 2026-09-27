namespace Mezon.Net.Client;

public sealed class MessageEmbedThumbnail
{
    public MessageEmbedThumbnail(string url) => Url = url;

    public string Url { get; }
}
