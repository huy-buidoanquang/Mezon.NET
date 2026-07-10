namespace Mezon.Net.Client
{
    /// <summary>
    /// Maps channel types to stream modes (aligned with mezon-js convertChanneltypeToChannelMode).
    /// </summary>
    public static class ChannelStreamModeHelper
    {
        public const int StreamModeChannel = 2;
        public const int StreamModeGroup = 3;
        public const int StreamModeDm = 4;
        public const int StreamModeThread = 6;

        public static int FromChannelType(int channelType)
        {
            return channelType switch
            {
                3 => StreamModeDm,
                2 => StreamModeGroup,
                7 => StreamModeThread,
                1 or 8 or 10 => StreamModeChannel,
                _ => 0,
            };
        }
    }
}
