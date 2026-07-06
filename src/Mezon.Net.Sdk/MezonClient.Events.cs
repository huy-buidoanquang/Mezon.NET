using System;
using System.Threading.Tasks;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Sdk.Agent;

namespace Mezon.Net.Sdk
{
    public sealed partial class MezonClient
    {
        public void OnChannelMessage(Func<ChannelMessage, Task> handler)
            => _engine.ChannelMessageEvent += handler;

        public void OnChannelCreated(Func<ChannelCreatedEvent, Task> handler)
            => _engine.ChannelCreatedEvent += handler;

        public void OnChannelUpdated(Func<ChannelUpdatedEvent, Task> handler)
            => _engine.ChannelUpdatedEvent += handler;

        public void OnChannelDeleted(Func<ChannelDeletedEvent, Task> handler)
            => _engine.ChannelDeletedEvent += handler;

        public void OnChannelMessage(Func<ChannelMessage, ValueTask> handler)
            => _engine.ChannelMessageEvent += message => handler(message).AsTask();

        public void OnMessageReaction(Func<MessageReaction, Task> handler)
            => _engine.MessageReactionEvent += handler;

        public void OnNotifications(Func<Notifications, Task> handler)
            => _engine.NotificationsEvent += handler;

        public void OnTokenSend(Func<Task> handler)
            => _engine.TokenSentEvent += handler;

        public void OnUserChannelRemoved(Func<Task> handler)
            => _engine.UserChannelRemovedEvent += handler;

        public void OnUserClanRemoved(Func<Task> handler)
            => _engine.UserClanRemovedEvent += handler;

        public void OnUserChannelAdded(Func<UserChannelAdded, Task> handler)
            => _engine.UserChannelAddedEvent += handler;

        public void OnGiveCoffee(Func<Task> handler)
            => _engine.GiveCoffeeEvent += handler;

        public void OnRoleEvent(Func<Task> handler)
            => _engine.RoleEvent += handler;

        public void OnRoleAssign(Func<Task> handler)
            => _engine.RoleAssignEvent += handler;

        public void OnAddClanUser(Func<Task> handler)
            => _engine.AddClanUserEvent += handler;

        public void OnClanEventCreated(Func<Task> handler)
            => _engine.ClanEventCreated += handler;

        public void OnMessageButtonClicked(Func<Task> handler)
            => _engine.MessageButtonClickedEvent += handler;

        public void OnStreamingJoined(Func<Task> handler)
            => _engine.StreamingJoinedEvent += handler;

        public void OnStreamingLeaved(Func<Task> handler)
            => _engine.StreamingLeavedEvent += handler;

        public void OnDropdownBoxSelected(Func<Task> handler)
            => _engine.DropdownBoxSelectedEvent += handler;

        public void OnWebrtcSignalingFwd(Func<Task> handler)
            => _engine.WebrtcSignalingFwdEvent += handler;

        public void OnVoiceStarted(Func<VoiceStartedEvent, Task> handler)
            => _engine.VoiceStartedEvent += handler;

        public void OnVoiceEnded(Func<VoiceEndedEvent, Task> handler)
            => _engine.VoiceEndedEvent += handler;

        public void OnVoiceJoined(Func<VoiceJoinedEvent, Task> handler)
            => _engine.VoiceJoinedEvent += handler;

        public void OnVoiceLeaved(Func<VoiceLeavedEvent, Task> handler)
            => _engine.VoiceLeavedEvent += handler;

        public void OnAIAgentEnable(Func<Task> handler)
            => _engine.AiagentEnabledEvent += handler;

        public void OnQuickMenu(Func<Task> handler)
            => _engine.QuickMenuEvent += handler;

        public void OnAIAgentSessionStarted(Func<AgentSseSessionEvent, Task> handler)
            => AgentSessionStarted += handler;

        public void OnAIAgentSessionEnded(Func<AgentSseSessionEvent, Task> handler)
            => AgentSessionEnded += handler;

        public void OnAIAgentSessionSummaryDone(Func<AgentSseSessionEvent, Task> handler)
            => AgentSessionSummaryDone += handler;

        internal event Func<AgentSseSessionEvent, Task>? AgentSessionStarted;
        internal event Func<AgentSseSessionEvent, Task>? AgentSessionEnded;
        internal event Func<AgentSseSessionEvent, Task>? AgentSessionSummaryDone;
    }
}
