namespace Mezon.Net.Client
{
    /// <summary>Hashtag token (<c>hg</c>) — channel mention span in UTF-16 offsets.</summary>
    public readonly record struct HashtagOnMessage(string? ChannelId, int? Start, int? End);
}
