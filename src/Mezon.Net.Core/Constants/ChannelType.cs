namespace Mezon.Net.Core
{
    public enum ChannelType
    {
        Channel = 1,
        Group = 2,
        Dm = 3,
        GmeetVoice = 4,
        Forum = 5,
        Streaming = 6,
        Thread = 7,
        App = 8,
        Announcement = 9,
        MezonVoice = 10,
    }

    public enum ChannelStreamMode
    {
        Channel = 2,
        Group = 3,
        Dm = 4,
        Clan = 5,
        Thread = 6,
    }

    public static class ChannelModeConverter
    {
        public static int ToStreamMode(int channelType)
        {
            return channelType switch
            {
                (int)ChannelType.Dm => (int)ChannelStreamMode.Dm,
                (int)ChannelType.Group => (int)ChannelStreamMode.Group,
                (int)ChannelType.Channel => (int)ChannelStreamMode.Channel,
                (int)ChannelType.App => (int)ChannelStreamMode.Channel,
                (int)ChannelType.MezonVoice => (int)ChannelStreamMode.Channel,
                (int)ChannelType.Thread => (int)ChannelStreamMode.Thread,
                _ => 0,
            };
        }
    }
}
