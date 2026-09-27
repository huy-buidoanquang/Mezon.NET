namespace Mezon.Net.Client;

public sealed class MessageEmbedImage
{
    public MessageEmbedImage(string url, string? width = null, string? height = null)
    {
        Url = url;
        Width = width;
        Height = height;
    }

    public string Url { get; }
    public string? Width { get; }
    public string? Height { get; }
}
