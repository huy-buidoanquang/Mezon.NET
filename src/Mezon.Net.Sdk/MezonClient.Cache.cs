using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Models;
using SdkClan = Mezon.Net.Sdk.Entities.Clan;
using SdkRole = Mezon.Net.Sdk.Entities.Role;
using SdkUser = Mezon.Net.Sdk.Entities.User;

namespace Mezon.Net.Sdk
{
    public sealed partial class MezonClient
    {
        /// <summary>Clans for which JoinClanChat has been scheduled/sent this process lifetime (idempotent RT joins).</summary>
        private readonly ConcurrentDictionary<long, byte> _joinedClanChats = new ConcurrentDictionary<long, byte>();

        /// <summary>RoleEvent status value treated as role deletion (Mezon role lifecycle).</summary>
        private const int RoleEventStatusDeleted = 3;

        private void BindCacheListeners()
        {
            if (_cacheListenersBound)
            {
                return;
            }

            _cacheListenersBound = true;
            _engine.ChannelMessageReceivedEvent += OnChannelMessageInternalAsync;
            _engine.ChannelMessageUpdatedEvent += OnChannelMessageUpdatedInternalAsync;
            _engine.ChannelMessageRemovedEvent += OnChannelMessageRemovedInternalAsync;
            _engine.MessageReactionReceivedEvent += OnMessageReactionInternalAsync;
            _engine.ChannelCreatedEvent += OnChannelCreatedInternalAsync;
            _engine.ChannelUpdatedEvent += OnChannelUpdatedInternalAsync;
            _engine.ChannelDeletedEvent += OnChannelDeletedInternalAsync;
            _engine.UserChannelAddedEvent += OnUserChannelAddedInternalAsync;
            _engine.UserChannelRemovedEvent += OnUserChannelRemovedInternalAsync;
            _engine.ClanJoinedEvent += OnClanJoinedInternalAsync;
            _engine.ClanUserAddedEvent += OnClanUserAddedInternalAsync;
            _engine.RoleChangedEvent += OnRoleChangedInternalAsync;
            _engine.RoleAssignedEvent += OnRoleAssignedInternalAsync;
        }

        private Task OnChannelMessageInternalAsync(ChannelMessageEventData messageEvent)
        {
            var message = (ChannelMessageResponse)messageEvent;
            if (message.ChannelId != 0 && Channels.TryGet(message.ChannelId, out var channel))
            {
                if (channel.Messages.TryGet(message.MessageId, out var existing))
                {
                    existing.UpdateFrom(message);
                }
                else
                {
                    channel.Messages.Set(message.MessageId, new Entities.Message(this, channel, message));
                }
            }

            UpdateUserFromMessage(message);
            return Task.CompletedTask;
        }

        private Task OnChannelMessageUpdatedInternalAsync(ChannelMessageUpdateEventData messageEvent)
        {
            var update = (ChannelMessageUpdateResponse)messageEvent;
            if (update.ChannelId == 0 || !Channels.TryGet(update.ChannelId, out var channel))
            {
                return Task.CompletedTask;
            }

            if (channel.Messages.TryGet(update.MessageId, out var existing))
            {
                existing.UpdateFrom(update);
            }

            return Task.CompletedTask;
        }

        private Task OnChannelMessageRemovedInternalAsync(ChannelMessageRemoveEventData messageEvent)
        {
            var remove = (ChannelMessageRemoveResponse)messageEvent;
            if (remove.ChannelId != 0 && Channels.TryGet(remove.ChannelId, out var channel))
            {
                channel.Messages.Remove(remove.MessageId);
            }

            return Task.CompletedTask;
        }

        private Task OnMessageReactionInternalAsync(MessageReactionEventData reactionEvent)
        {
            var reaction = (MessageReactionResponse)reactionEvent;
            if (reaction.ChannelId == 0 || !Channels.TryGet(reaction.ChannelId, out var channel))
            {
                return Task.CompletedTask;
            }

            if (channel.Messages.TryGet(reaction.MessageId, out var existing))
            {
                existing.ApplyReaction(reaction);
            }

            return Task.CompletedTask;
        }

        private void UpdateUserFromMessage(ChannelMessageResponse message)
        {
            if (message.SenderId == 0)
            {
                return;
            }

            if (Users.TryGet(message.SenderId, out var user))
            {
                user.Username = message.Username;
                user.DisplayName = message.DisplayName;
                user.ClanNick = message.ClanNick;
            }
            else
            {
                Users.Set(message.SenderId, new SdkUser(this, message.SenderId, message.Username, message.DisplayName, message.ClanNick));
            }
        }

        private void UpsertUserFromProfile(UserProfileRedisResponse profile)
        {
            if (profile.UserId == 0)
            {
                return;
            }

            if (Users.TryGet(profile.UserId, out var user))
            {
                if (!string.IsNullOrEmpty(profile.Username))
                {
                    user.Username = profile.Username;
                }

                if (!string.IsNullOrEmpty(profile.DisplayName))
                {
                    user.DisplayName = profile.DisplayName;
                }
            }
            else
            {
                Users.Set(profile.UserId, new SdkUser(this, profile.UserId, profile.Username, profile.DisplayName));
            }
        }

        private Task OnChannelCreatedInternalAsync(ChannelCreatedEventEventData evt)
        {
            var data = (ChannelCreatedEventResponse)evt;
            UpdateChannelCache(data.ClanId, data.ChannelId, data.ChannelLabel, data.ChannelType, data.ChannelPrivate == 0);
            return Task.CompletedTask;
        }

        private Task OnChannelUpdatedInternalAsync(ChannelUpdatedEventEventData evt)
        {
            var data = (ChannelUpdatedEventResponse)evt;
            if (data.ChannelType == (int)ChannelType.Thread && data.Status == 1)
            {
                ScheduleBackground(
                    _engine.JoinChannelChatRtAsync(new ChannelJoinParams(data.ClanId, data.ChannelId, data.ChannelType, !data.ChannelPrivate)),
                    "JoinChannelChat (thread update)");
                return Task.CompletedTask;
            }

            UpdateChannelCache(data.ClanId, data.ChannelId, data.ChannelLabel, data.ChannelType, !data.ChannelPrivate);
            return Task.CompletedTask;
        }

        private Task OnChannelDeletedInternalAsync(ChannelDeletedEventEventData evt)
        {
            var data = (ChannelDeletedEventResponse)evt;
            Channels.Remove(data.ChannelId);
            return Task.CompletedTask;
        }

        private Task OnClanJoinedInternalAsync(ClanJoinEventData evt)
        {
            var data = (ClanJoinResponse)evt;
            if (data.ClanId != 0)
            {
                EnsureClanJoined(data.ClanId);
            }

            return Task.CompletedTask;
        }

        private Task OnClanUserAddedInternalAsync(AddClanUserEventEventData evt)
        {
            var data = (AddClanUserEventResponse)evt;
            UpsertUserFromProfile(data.User);
            if (data.User.UserId == Options.BotId && data.ClanId != 0)
            {
                EnsureClanJoined(data.ClanId);
            }

            return Task.CompletedTask;
        }

        private Task OnUserChannelAddedInternalAsync(UserChannelAddedEventData channelEvent)
        {
            var data = (UserChannelAddedResponse)channelEvent;
            var channelDesc = data.ChannelDesc;
            if (channelDesc.ChannelId == 0)
            {
                return Task.CompletedTask;
            }

            var clanId = data.ClanId != 0 ? data.ClanId : channelDesc.ClanId;
            if (clanId != 0)
            {
                EnsureClanStub(clanId);
            }

            UpsertChannelFromDescription(channelDesc.Proto);

            for (var i = 0; i < data.Users.Count; i++)
            {
                var user = data.Users[i];
                UpsertUserFromProfile(user);
                if (user.UserId == Options.BotId)
                {
                    ScheduleBackground(
                        _engine.JoinChannelChatRtAsync(new ChannelJoinParams(
                            clanId,
                            channelDesc.ChannelId,
                            channelDesc.Type,
                            channelDesc.ChannelPrivate == 0)),
                        "JoinChannelChat (user channel added)");
                    break;
                }
            }

            return Task.CompletedTask;
        }

        private Task OnUserChannelRemovedInternalAsync(UserChannelRemovedEventData channelEvent)
        {
            var data = (UserChannelRemovedResponse)channelEvent;
            var botRemoved = false;
            for (var i = 0; i < data.UserIds.Count; i++)
            {
                if (data.UserIds[i] == Options.BotId)
                {
                    botRemoved = true;
                    break;
                }
            }

            if (!botRemoved)
            {
                return Task.CompletedTask;
            }

            var isPublic = true;
            if (Channels.TryGet(data.ChannelId, out var channel))
            {
                isPublic = channel.IsPublic;
            }

            var channelType = data.ChannelType != 0
                ? data.ChannelType
                : channel?.Type ?? (int)ChannelType.Channel;

            ScheduleBackground(
                _engine.LeaveChannelChatRtAsync(new ChannelLeaveParams(data.ClanId, data.ChannelId, channelType, isPublic)),
                "LeaveChannelChat (user channel removed)");
            Channels.Remove(data.ChannelId);
            return Task.CompletedTask;
        }

        private Task OnRoleChangedInternalAsync(RoleEventEventData evt)
        {
            var data = (RoleEventResponse)evt;
            var roleProto = data.Role.Proto;
            if (roleProto.Id == 0)
            {
                return Task.CompletedTask;
            }

            if (data.Status == RoleEventStatusDeleted)
            {
                Roles.Remove(roleProto.Id);
                return Task.CompletedTask;
            }

            if (Roles.TryGet(roleProto.Id, out var existing))
            {
                existing.UpdateFrom(roleProto);
            }
            else
            {
                Roles.Set(roleProto.Id, new SdkRole(roleProto));
            }

            if (Roles.TryGet(roleProto.Id, out var role))
            {
                role.ApplyAssigned(data.UserAddIds);
                role.ApplyRemoved(data.UserRemoveIds);
            }

            return Task.CompletedTask;
        }

        private Task OnRoleAssignedInternalAsync(RoleAssignedEventEventData evt)
        {
            var data = (RoleAssignedEventResponse)evt;
            if (data.RoleId == 0)
            {
                return Task.CompletedTask;
            }

            if (!long.TryParse(data.ClanId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var clanId))
            {
                clanId = 0;
            }

            if (!Roles.TryGet(data.RoleId, out var role))
            {
                role = new SdkRole(data.RoleId, clanId);
                Roles.Set(data.RoleId, role);
            }

            role.ApplyAssigned(data.UserIdsAssigned);
            role.ApplyRemoved(data.UserIdsRemoved);
            return Task.CompletedTask;
        }

        /// <summary>Ensure clan exists in L1 and JoinClanChat is scheduled once (no REST).</summary>
        private void EnsureClanJoined(long clanId)
        {
            EnsureClanStub(clanId);
            if (!_joinedClanChats.TryAdd(clanId, 0))
            {
                return;
            }

            ScheduleBackground(
                _engine.JoinClanChatRtAsync(new ClanJoinParams(clanId)),
                "JoinClanChat (ensure clan joined)");
        }

        private SdkClan EnsureClanStub(long clanId)
        {
            if (Clans.TryGet(clanId, out var clan))
            {
                return clan;
            }

            clan = new SdkClan(this, new ClanDesc { ClanId = clanId });
            Clans.Set(clanId, clan);
            return clan;
        }

        private void ScheduleBackground(Task task, string name)
        {
            _ = ObserveFaultAsync(task, name);
        }

        private async Task ObserveFaultAsync(Task task, string name)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.WarningAsync($"Background {name} failed.", ex).ConfigureAwait(false);
            }
        }

        private void UpdateChannelCache(long clanId, long channelId, string label, int type, bool isPublic)
        {
            var desc = new ChannelDescription
            {
                ClanId = clanId,
                ChannelId = channelId,
                ChannelLabel = label,
                Type = type,
                ChannelPrivate = isPublic ? 0 : 1,
            };

            if (Channels.TryGet(channelId, out var existing))
            {
                // Preserve richer fields already cached when event payload is sparse.
                var current = existing.Proto;
                if (current.ParentId != 0 && desc.ParentId == 0)
                {
                    desc.ParentId = current.ParentId;
                }

                if (current.CategoryId != 0 && desc.CategoryId == 0)
                {
                    desc.CategoryId = current.CategoryId;
                }

                if (!string.IsNullOrEmpty(current.CategoryName) && string.IsNullOrEmpty(desc.CategoryName))
                {
                    desc.CategoryName = current.CategoryName;
                }

                if (!string.IsNullOrEmpty(current.MeetingCode) && string.IsNullOrEmpty(desc.MeetingCode))
                {
                    desc.MeetingCode = current.MeetingCode;
                }

                if (current.CreatorId != 0 && desc.CreatorId == 0)
                {
                    desc.CreatorId = current.CreatorId;
                }

                if (current.AppId != 0 && desc.AppId == 0)
                {
                    desc.AppId = current.AppId;
                }

                existing.UpdateFrom(desc);
                return;
            }

            UpsertChannelFromDescription(desc);
        }

        internal void MarkClanChatJoined(long clanId) => _joinedClanChats.TryAdd(clanId, 0);
    }
}
