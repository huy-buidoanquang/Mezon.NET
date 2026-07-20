using System;
using System.Threading.Tasks;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Agent;

namespace Mezon.Net.Sdk
{
    public sealed partial class MezonClient
    {
        public event Func<ChannelMessageEventData, Task> ChannelMessageReceived
        {
            add => _engine.ChannelMessageReceivedEvent += value;
            remove => _engine.ChannelMessageReceivedEvent -= value;
        }

        public event Func<ChannelCreatedEventEventData, Task> ChannelCreated
        {
            add => _engine.ChannelCreatedEvent += value;
            remove => _engine.ChannelCreatedEvent -= value;
        }

        public event Func<ChannelUpdatedEventEventData, Task> ChannelUpdated
        {
            add => _engine.ChannelUpdatedEvent += value;
            remove => _engine.ChannelUpdatedEvent -= value;
        }

        public event Func<ChannelDeletedEventEventData, Task> ChannelDeleted
        {
            add => _engine.ChannelDeletedEvent += value;
            remove => _engine.ChannelDeletedEvent -= value;
        }

        public event Func<MessageReactionEventData, Task> MessageReactionReceived
        {
            add => _engine.MessageReactionReceivedEvent += value;
            remove => _engine.MessageReactionReceivedEvent -= value;
        }

        public event Func<NotificationsEventData, Task> NotificationsReceived
        {
            add => _engine.NotificationsReceivedEvent += value;
            remove => _engine.NotificationsReceivedEvent -= value;
        }

        public event Func<Task> TokenSent
        {
            add => _engine.TokenSentEvent += value;
            remove => _engine.TokenSentEvent -= value;
        }

        public event Func<Task> UserChannelRemoved
        {
            add => _engine.UserChannelRemovedEvent += value;
            remove => _engine.UserChannelRemovedEvent -= value;
        }

        public event Func<Task> UserClanRemoved
        {
            add => _engine.UserClanRemovedEvent += value;
            remove => _engine.UserClanRemovedEvent -= value;
        }

        public event Func<UserChannelAddedEventData, Task> UserChannelAdded
        {
            add => _engine.UserChannelAddedEvent += value;
            remove => _engine.UserChannelAddedEvent -= value;
        }

        public event Func<Task> CoffeeGiven
        {
            add => _engine.CoffeeGivenEvent += value;
            remove => _engine.CoffeeGivenEvent -= value;
        }

        public event Func<Task> RoleChanged
        {
            add => _engine.RoleChangedEvent += value;
            remove => _engine.RoleChangedEvent -= value;
        }

        public event Func<Task> RoleAssigned
        {
            add => _engine.RoleAssignedEvent += value;
            remove => _engine.RoleAssignedEvent -= value;
        }

        public event Func<Task> ClanUserAdded
        {
            add => _engine.ClanUserAddedEvent += value;
            remove => _engine.ClanUserAddedEvent -= value;
        }

        public event Func<Task> ClanEventCreated
        {
            add => _engine.ClanEventCreated += value;
            remove => _engine.ClanEventCreated -= value;
        }

        public event Func<MessageButtonClickedEventData, Task> MessageButtonClicked
        {
            add => _engine.MessageButtonClickedEvent += value;
            remove => _engine.MessageButtonClickedEvent -= value;
        }

        public event Func<Task> StreamingJoined
        {
            add => _engine.StreamingJoinedEvent += value;
            remove => _engine.StreamingJoinedEvent -= value;
        }

        public event Func<Task> StreamingLeaved
        {
            add => _engine.StreamingLeavedEvent += value;
            remove => _engine.StreamingLeavedEvent -= value;
        }

        public event Func<DropdownBoxSelectedEventData, Task> DropdownBoxSelected
        {
            add => _engine.DropdownBoxSelectedEvent += value;
            remove => _engine.DropdownBoxSelectedEvent -= value;
        }

        public event Func<Task> WebrtcSignalingForwarded
        {
            add => _engine.WebrtcSignalingForwardedEvent += value;
            remove => _engine.WebrtcSignalingForwardedEvent -= value;
        }

        public event Func<VoiceStartedEventEventData, Task> VoiceStarted
        {
            add => _engine.VoiceStartedEvent += value;
            remove => _engine.VoiceStartedEvent -= value;
        }

        public event Func<VoiceEndedEventEventData, Task> VoiceEnded
        {
            add => _engine.VoiceEndedEvent += value;
            remove => _engine.VoiceEndedEvent -= value;
        }

        public event Func<VoiceJoinedEventEventData, Task> VoiceJoined
        {
            add => _engine.VoiceJoinedEvent += value;
            remove => _engine.VoiceJoinedEvent -= value;
        }

        public event Func<VoiceLeavedEventEventData, Task> VoiceLeaved
        {
            add => _engine.VoiceLeavedEvent += value;
            remove => _engine.VoiceLeavedEvent -= value;
        }

        public event Func<Task> AIAgentEnabled
        {
            add => _engine.AIAgentEnabledEvent += value;
            remove => _engine.AIAgentEnabledEvent -= value;
        }

        public event Func<Task> QuickMenuReceived
        {
            add => _engine.QuickMenuReceivedEvent += value;
            remove => _engine.QuickMenuReceivedEvent -= value;
        }

        public event Func<AgentSseSessionEvent, Task> AgentSessionStarted
        {
            add => AgentSessionStartedInternal += value;
            remove => AgentSessionStartedInternal -= value;
        }

        public event Func<AgentSseSessionEvent, Task> AgentSessionEnded
        {
            add => AgentSessionEndedInternal += value;
            remove => AgentSessionEndedInternal -= value;
        }

        public event Func<AgentSseSessionEvent, Task> AgentSessionSummaryDone
        {
            add => AgentSessionSummaryDoneInternal += value;
            remove => AgentSessionSummaryDoneInternal -= value;
        }

        internal event Func<AgentSseSessionEvent, Task>? AgentSessionStartedInternal;
        internal event Func<AgentSseSessionEvent, Task>? AgentSessionEndedInternal;
        internal event Func<AgentSseSessionEvent, Task>? AgentSessionSummaryDoneInternal;
    }
}
