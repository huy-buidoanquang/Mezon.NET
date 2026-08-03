namespace Mezon.Net.Client
{
    /// <summary>
    /// Wire values for <see cref="MarkdownOnMessage.Type"/> (mezon <c>EBacktickType</c>).
    /// These markers live under the <c>mk</c> array — not as sibling root properties.
    /// </summary>
    public static class MarkdownMarkerType
    {
        public const string Triple = "t";
        public const string Single = "s";
        public const string Pre = "pre";
        public const string Code = "c";
        public const string Bold = "b";
        public const string Link = "lk";
        public const string VoiceLink = "vk";
        public const string LinkYoutube = "lk_yt";
        public const string LinkFacebook = "lk_fb";
        public const string LinkTikTok = "lk_tt";
        public const string OgpPreview = "lk_ogp";
    }
}
