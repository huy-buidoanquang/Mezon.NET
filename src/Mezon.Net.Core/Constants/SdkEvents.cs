namespace Mezon.Net.Core.Constants
{
    /// <summary>
    /// High-level SDK event names aligned with mezon-sdk TypeScript <c>Events</c> enum.
    /// </summary>
    public enum SdkEvent
    {
        ChannelMessage,
        ChannelCreated,
        ChannelUpdated,
        ChannelDeleted,
        ChannelArchive,
        TokenSend,
        MessageReaction,
        UserChannelRemoved,
        UserClanRemoved,
        UserChannelAdded,
        GiveCoffee,
        RoleEvent,
        RoleAssign,
        Notifications,
        AddClanUser,
        ClanEventCreated,
        MessageButtonClicked,
        StreamingJoinedEvent,
        StreamingLeavedEvent,
        DropdownBoxSelected,
        WebrtcSignalingFwd,
        VoiceStartedEvent,
        VoiceEndedEvent,
        VoiceJoinedEvent,
        VoiceLeavedEvent,
        AIAgentEnable,
        QuickMenu,
        AIAgentSessionStarted,
        AIAgentSessionEnded,
        AIAgentSessionSummaryDone,
    }
}
