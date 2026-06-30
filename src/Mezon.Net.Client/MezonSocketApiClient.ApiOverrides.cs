using System.Collections.Generic;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Google.Protobuf.WellKnownTypes;
using Mezon.Net.Utils;

namespace Mezon.Net.Client
{
    internal partial class MezonSocketApiClient
    {
        public async Task DeleteAccountAsync(RequestOptions? options = null)
        {
            await SendApiAsync("DeleteAccount", new Empty(), Empty.Parser, options);
        }

        public Task<Account> GetAccountAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetAccount", new Empty(), Account.Parser, options);
        }

        public Task<AddFriendsResponse> AddFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            var request = new AddFriendsRequest();
            if (ids != null)
            {
            foreach (var id in ids)
            {
            request.Ids.Add(id);
            }
            }
            if (usernames != null)
            {
            foreach (var username in usernames)
            {
            request.Usernames.Add(username);
            }
            }
            return SendApiAsync("AddFriends", request, AddFriendsResponse.Parser, options);
        }

        public async Task BlockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            var request = new BlockFriendsRequest();
            if (ids != null)
            {
            foreach (var id in ids)
            {
            request.Ids.Add(id);
            }
            }
            if (usernames != null)
            {
            foreach (var username in usernames)
            {
            request.Usernames.Add(username);
            }
            }
            await SendApiAsync("BlockFriends", request, Empty.Parser, options);
        }

        public async Task UnblockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            var request = new BlockFriendsRequest();
            if (ids != null)
            {
            foreach (var id in ids)
            {
            request.Ids.Add(id);
            }
            }
            if (usernames != null)
            {
            foreach (var username in usernames)
            {
            request.Usernames.Add(username);
            }
            }
            await SendApiAsync("UnblockFriends", request, Empty.Parser, options);
        }

        public async Task DeleteFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            var request = new DeleteFriendsRequest();
            if (ids != null)
            {
            foreach (var id in ids)
            {
            request.Ids.Add(id);
            }
            }
            if (usernames != null)
            {
            foreach (var username in usernames)
            {
            request.Usernames.Add(username);
            }
            }
            await SendApiAsync("DeleteFriends", request, Empty.Parser, options);
        }

        public Task<FriendList> ListFriendsAsync(int? state = null, int? limit = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new ListFriendsRequest();
            if (state.HasValue)
            {
            request.State = state.Value;
            }
            if (limit.HasValue)
            {
            request.Limit = limit.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
            request.Cursor = cursor;
            }
            return SendApiAsync("ListFriends", request, FriendList.Parser, options);
        }

        public Task<ClanDesc> CreateClanDescAsync(string clanName, string? logo = null, string? banner = null, RequestOptions? options = null)
        {
            Check.NotNullOrEmpty(clanName, nameof(clanName));
            var request = new CreateClanDescRequest();
            request.ClanName = clanName;
            if (!string.IsNullOrEmpty(logo))
            {
            request.Logo = logo;
            }
            if (!string.IsNullOrEmpty(banner))
            {
            request.Banner = banner;
            }
            return SendApiAsync("CreateClanDesc", request, ClanDesc.Parser, options);
        }

        public async Task DeleteClanDescAsync(long clanId, RequestOptions? options = null)
        {
            var request = new DeleteClanDescRequest();
            request.ClanDescId = clanId;
            await SendApiAsync("DeleteClanDesc", request, Empty.Parser, options);
        }

        public async Task UpdateClanDescAsync(UpdateClanDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanDesc", body, Empty.Parser, options);
        }

        public Task<ClanUserList> ListClanUsersAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListClanUsersRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListClanUsers", request, ClanUserList.Parser, options);
        }

        public async Task RemoveClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new RemoveClanUsersRequest();
            request.ClanId = clanId;
            foreach (var userId in userIds)
            {
            request.UserIds.Add(userId);
            }
            await SendApiAsync("RemoveClanUsers", request, Empty.Parser, options);
        }

        public async Task BanClanUsersAsync(long clanId, long channelId, IEnumerable<long> userIds, int? banTime = null, string? reason = null, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new BanClanUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            foreach (var userId in userIds)
            {
            request.UserIds.Add(userId);
            }
            if (banTime.HasValue)
            {
            request.BanTime = banTime.Value;
            }
            if (!string.IsNullOrEmpty(reason))
            {
            request.Reason = reason;
            }
            await SendApiAsync("BanClanUsers", request, Empty.Parser, options);
        }

        public Task<Internal.Api.ChannelDescription> CreateChannelDescAsync(CreateChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateChannelDesc", body, Internal.Api.ChannelDescription.Parser, options);
        }

        public async Task DeleteChannelDescAsync(long channelId, RequestOptions? options = null)
        {
            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;
            await SendApiAsync("DeleteChannelDesc", request, Empty.Parser, options);
        }

        public async Task UpdateChannelDescAsync(UpdateChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateChannelDesc", body, Empty.Parser, options);
        }

        public async Task AddChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new AddChannelUsersRequest();
            request.ChannelId = channelId;
            foreach (var userId in userIds)
            {
            request.UserIds.Add(userId);
            }
            await SendApiAsync("AddChannelUsers", request, Empty.Parser, options);
        }

        public async Task RemoveChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new RemoveChannelUsersRequest();
            request.ChannelId = channelId;
            foreach (var userId in userIds)
            {
            request.UserIds.Add(userId);
            }
            await SendApiAsync("RemoveChannelUsers", request, Empty.Parser, options);
        }

        public Task<ChannelMessageList> ListChannelMessagesAsync(long clanId, long channelId, long? messageId = null, int? direction = null, int? limit = null, long? topicId = null, RequestOptions? options = null)
        {
            var request = new ListChannelMessagesRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            if (messageId.HasValue)
            {
            request.MessageId = messageId.Value;
            }
            if (direction.HasValue)
            {
            request.Direction = direction.Value;
            }
            if (limit.HasValue)
            {
            request.Limit = limit.Value;
            }
            if (topicId.HasValue)
            {
            request.TopicId = topicId.Value;
            }
            return SendApiAsync("ListChannelMessages", request, ChannelMessageList.Parser, options);
        }

        public Task<ChannelUserList> ListChannelUsersAsync(long clanId, long channelId, int channelType, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;
            if (limit.HasValue)
            {
            request.Limit = limit.Value;
            }
            if (state.HasValue)
            {
            request.State = state.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
            request.Cursor = cursor;
            }
            return SendApiAsync("ListChannelUsers", request, ChannelUserList.Parser, options);
        }

        public async Task DeleteRoleAsync(long roleId, RequestOptions? options = null)
        {
            var request = new DeleteRoleRequest();
            request.RoleId = roleId;
            await SendApiAsync("DeleteRole", request, Empty.Parser, options);
        }

        public Task<RoleListEventResponse> ListRolesAsync(long? clanId = null, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new RoleListEventRequest();
            if (clanId.HasValue)
            {
            request.ClanId = clanId.Value;
            }
            if (limit.HasValue)
            {
            request.Limit = limit.Value;
            }
            if (state.HasValue)
            {
            request.State = state.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
            request.Cursor = cursor;
            }
            return SendApiAsync("ListRoles", request, RoleListEventResponse.Parser, options);
        }

        public async Task UpdateUserAsync(UpdateUsersRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateUser", body, Empty.Parser, options);
        }

        public async Task DeleteEventAsync(long eventId, RequestOptions? options = null)
        {
            var request = new DeleteEventRequest();
            request.EventId = eventId;
            await SendApiAsync("DeleteEvent", request, Empty.Parser, options);
        }

        public Task<EventList> ListEventsAsync(long? clanId = null, RequestOptions? options = null)
        {
            var request = new ListEventsRequest();
            if (clanId.HasValue)
            {
            request.ClanId = clanId.Value;
            }
            return SendApiAsync("ListEvents", request, EventList.Parser, options);
        }

        public Task<ChannelMessage> CreatePinMessageAsync(PinMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreatePinMessage", body, ChannelMessage.Parser, options);
        }

        public Task<PinMessagesList> GetPinMessagesListAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            var request = new PinMessageRequest();
            request.ChannelId = channelId;
            request.ClanId = clanId;
            return SendApiAsync("GetPinMessagesList", request, PinMessagesList.Parser, options);
        }

        public async Task DeletePinMessageAsync(long messageId, long channelId, long clanId, RequestOptions? options = null)
        {
            var request = new DeletePinMessage();
            request.MessageId = messageId;
            request.ChannelId = channelId;
            request.ClanId = clanId;
            await SendApiAsync("DeletePinMessage", request, Empty.Parser, options);
        }

        public async Task MarkAsReadAsync(MarkAsReadRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("MarkAsRead", body, Empty.Parser, options);
        }

        public async Task CreateClanEmojiAsync(ClanEmojiCreateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("CreateClanEmoji", body, Empty.Parser, options);
        }

        public async Task UpdateClanEmojiByIdAsync(ClanEmojiUpdateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanEmojiById", body, Empty.Parser, options);
        }

        public async Task DeleteClanEmojiByIdAsync(long emojiId, long clanId, RequestOptions? options = null)
        {
            var request = new ClanEmojiDeleteRequest();
            request.Id = emojiId;
            request.ClanId = clanId;
            await SendApiAsync("DeleteByIdClanEmoji", request, Empty.Parser, options);
        }

        public async Task AddClanStickerAsync(ClanStickerAddRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddClanSticker", body, Empty.Parser, options);
        }

        public async Task UpdateClanStickerByIdAsync(ClanStickerUpdateByIdRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanStickerById", body, Empty.Parser, options);
        }

        public async Task DeleteClanStickerByIdAsync(long stickerId, long clanId, RequestOptions? options = null)
        {
            var request = new ClanStickerDeleteRequest();
            request.Id = stickerId;
            request.ClanId = clanId;
            await SendApiAsync("DeleteClanStickerById", request, Empty.Parser, options);
        }

        public Task<EmojiListedResponse> GetListEmojisByUserIdAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetListEmojisByUserId", new Empty(), EmojiListedResponse.Parser, options);
        }

        public Task<StickerListedResponse> GetListStickersByUserIdAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetListStickersByUserId", new Empty(), StickerListedResponse.Parser, options);
        }

        public Task<WebhookGenerateResponse> GenerateWebhookAsync(WebhookCreateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GenerateWebhook", body, WebhookGenerateResponse.Parser, options);
        }

        public Task<WebhookListResponse> ListWebhookByChannelIdAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            var request = new WebhookListRequest();
            request.ChannelId = channelId;
            request.ClanId = clanId;
            return SendApiAsync("ListWebhookByChannelId", request, WebhookListResponse.Parser, options);
        }

        public async Task UpdateWebhookByIdAsync(WebhookUpdateRequestById body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateWebhookById", body, Empty.Parser, options);
        }

        public async Task DeleteWebhookByIdAsync(WebhookDeleteRequestById body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteWebhookById", body, Empty.Parser, options);
        }

        public async Task CreateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("CreateSystemMessage", body, Empty.Parser, options);
        }

        public async Task UpdateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateSystemMessage", body, Empty.Parser, options);
        }

        public Task<SystemMessage> GetSystemMessageByClanIdAsync(long clanId, RequestOptions? options = null)
        {
            var request = new GetSystemMessage();
            request.ClanId = clanId;
            return SendApiAsync("GetSystemMessageByClanId", request, SystemMessage.Parser, options);
        }

        public async Task DeleteSystemMessageAsync(long clanId, RequestOptions? options = null)
        {
            var request = new DeleteSystemMessage();
            request.ClanId = clanId;
            await SendApiAsync("DeleteSystemMessage", request, Empty.Parser, options);
        }

        public async Task UpdateRoleOrderAsync(UpdateRoleOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateRoleOrder", body, Empty.Parser, options);
        }

        public async Task UpdateClanOrderAsync(UpdateClanOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanOrder", body, Empty.Parser, options);
        }

        public Task<ChanEncryptionMethod> GetChanEncryptionMethodAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ChanEncryptionMethod();
            request.ChannelId = channelId;
            return SendApiAsync("GetChanEncryptionMethod", request, ChanEncryptionMethod.Parser, options);
        }

        public async Task SetChanEncryptionMethodAsync(ChanEncryptionMethod body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetChanEncryptionMethod", body, Empty.Parser, options);
        }

        public Task<GetPubKeysResponse> GetPublicKeysAsync(IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new GetPubKeysRequest();
            foreach (var userId in userIds)
            {
            request.UserIds.Add(userId);
            }
            return SendApiAsync("GetPubKeys", request, GetPubKeysResponse.Parser, options);
        }

        public async Task PushPublicKeyAsync(PushPubKeyRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("PushPubKey", body, Empty.Parser, options);
        }

        public Task<GetKeyServerResp> GetKeyServerAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetKeyServer", new Empty(), GetKeyServerResp.Parser, options);
        }

        public Task<ListOnboardingResponse> ListOnboardingAsync(long clanId, int? guideType = null, RequestOptions? options = null)
        {
            var request = new ListOnboardingRequest();
            request.ClanId = clanId;
            if (guideType.HasValue)
            {
            request.GuideType = guideType.Value;
            }
            return SendApiAsync("ListOnboarding", request, ListOnboardingResponse.Parser, options);
        }

        public Task<OnboardingItem> GetOnboardingDetailAsync(long id, long clanId, RequestOptions? options = null)
        {
            var request = new OnboardingRequest();
            request.Id = id;
            request.ClanId = clanId;
            return SendApiAsync("GetOnboardingDetail", request, OnboardingItem.Parser, options);
        }

        public Task<ListOnboardingResponse> CreateOnboardingAsync(CreateOnboardingRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateOnboarding", body, ListOnboardingResponse.Parser, options);
        }

        public async Task UpdateOnboardingAsync(UpdateOnboardingRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateOnboarding", body, Empty.Parser, options);
        }

        public async Task DeleteOnboardingAsync(long id, long clanId, RequestOptions? options = null)
        {
            var request = new OnboardingRequest();
            request.Id = id;
            request.ClanId = clanId;
            await SendApiAsync("DeleteOnboarding", request, Empty.Parser, options);
        }

        public Task<ListUserActivity> ListActivityAsync(RequestOptions? options = null)
        {
            return SendApiAsync("ListActivity", new Empty(), ListUserActivity.Parser, options);
        }

        public Task<GenerateMeetTokenResponse> GenerateMeetTokenAsync(GenerateMeetTokenRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GenerateMeetToken", body, GenerateMeetTokenResponse.Parser, options);
        }

        public async Task TransferOwnershipAsync(TransferOwnershipRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("TransferOwnership", body, Empty.Parser, options);
        }

        public Task<PermissionList> GetListPermissionAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetListPermission", new Empty(), PermissionList.Parser, options);
        }

        public Task<PermissionList> ListRolePermissionsAsync(long roleId, RequestOptions? options = null)
        {
            var request = new ListPermissionsRequest();
            request.RoleId = roleId;
            return SendApiAsync("ListRolePermissions", request, PermissionList.Parser, options);
        }

        public Task<RoleUserList> ListRoleUsersAsync(long roleId, int? limit = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new ListRoleUsersRequest();
            request.RoleId = roleId;
            if (limit.HasValue)
            {
            request.Limit = limit.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
            request.Cursor = cursor;
            }
            return SendApiAsync("ListRoleUsers", request, RoleUserList.Parser, options);
        }

        public Task<UserPermissionInChannelListResponse> ListUserPermissionInChannelAsync(long clanId, long channelId, RequestOptions? options = null)
        {
            var request = new UserPermissionInChannelListRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            return SendApiAsync("ListUserPermissionInChannel", request, UserPermissionInChannelListResponse.Parser, options);
        }

        public async Task DeleteNotificationsAsync(IEnumerable<long>? ids = null, int? category = null, RequestOptions? options = null)
        {
            var request = new DeleteNotificationsRequest();
            if (ids != null)
            {
            foreach (var id in ids)
            {
            request.Ids.Add(id);
            }
            }
            if (category.HasValue)
            {
            request.Category = category.Value;
            }
            await SendApiAsync("DeleteNotifications", request, Empty.Parser, options);
        }

        public Task<NotificationList> ListNotificationsAsync(long? clanId = null, long? notificationId = null, int? limit = null, int? direction = null, RequestOptions? options = null)
        {
            var request = new ListNotificationsRequest();
            if (clanId.HasValue)
            {
            request.ClanId = clanId.Value;
            }
            if (notificationId.HasValue)
            {
            request.NotificationId = notificationId.Value;
            }
            if (limit.HasValue)
            {
            request.Limit = limit.Value;
            }
            if (direction.HasValue)
            {
            request.Direction = direction.Value;
            }
            return SendApiAsync("ListNotifications", request, NotificationList.Parser, options);
        }

        public Task<CategoryDesc> CreateCategoryDescAsync(CreateCategoryDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateCategoryDesc", body, CategoryDesc.Parser, options);
        }

        public async Task DeleteCategoryDescAsync(long categoryId, long clanId, RequestOptions? options = null)
        {
            var request = new DeleteCategoryDescRequest();
            request.CategoryId = categoryId;
            request.ClanId = clanId;
            await SendApiAsync("DeleteCategoryDesc", request, Empty.Parser, options);
        }

        public async Task UpdateCategoryAsync(UpdateCategoryDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateCategory", body, Empty.Parser, options);
        }

        public Task<CategoryDescList> ListCategoryDescsAsync(long clanId, RequestOptions? options = null)
        {
            var request = new CategoryDesc();
            request.ClanId = clanId;
            return SendApiAsync("ListCategoryDescs", request, CategoryDescList.Parser, options);
        }

        public Task<InviteUserRes> InviteUserAsync(long inviteId, RequestOptions? options = null)
        {
            var request = new InviteUserRequest();
            request.InviteId = inviteId;
            return SendApiAsync("InviteUser", request, InviteUserRes.Parser, options);
        }

        public async Task SetNotificationChannelSettingAsync(SetNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetNotificationChannelSetting", body, Empty.Parser, options);
        }

        public async Task SetMuteNotificationCategoryAsync(SetMuteRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetMuteCategory", body, Empty.Parser, options);
        }

        public async Task SetMuteNotificationChannelAsync(SetMuteRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetMuteChannel", body, Empty.Parser, options);
        }

        public Task<NotificationChannelCategorySettingList> GetChannelCategoryNotificationSettingsAsync(long clanId, RequestOptions? options = null)
        {
            var request = new NotificationClan();
            request.ClanId = clanId;
            return SendApiAsync("GetChannelCategoryNotiSettingsList", request, NotificationChannelCategorySettingList.Parser, options);
        }

        public Task<NotificationSetting> GetClanNotificationSettingAsync(long clanId, RequestOptions? options = null)
        {
            var request = new NotificationClan();
            request.ClanId = clanId;
            return SendApiAsync("GetNotificationClan", request, NotificationSetting.Parser, options);
        }

        public Task<UserStatus> GetUserStatusAsync(RequestOptions? options = null)
        {
            return SendApiAsync("GetUserStatus", new Empty(), UserStatus.Parser, options);
        }

        public async Task UpdateUserStatusAsync(UserStatusUpdate body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateUserStatus", body, Empty.Parser, options);
        }

        public Task<AppList> ListAppsAsync(string? filter = null, bool? tombstones = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new ListAppsRequest();
            if (!string.IsNullOrEmpty(filter))
            {
            request.Filter = filter;
            }
            if (tombstones.HasValue)
            {
            request.Tombstones = tombstones.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
            request.Cursor = cursor;
            }
            return SendApiAsync("ListApps", request, AppList.Parser, options);
        }

        public Task<App> GetAppAsync(long id, RequestOptions? options = null)
        {
            var request = new AppId();
            request.Id = id;
            return SendApiAsync("GetApp", request, App.Parser, options);
        }

        public Task<App> UpdateAppAsync(UpdateAppRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("UpdateApp", body, App.Parser, options);
        }

        public async Task DeleteAppAsync(long id, bool? recordDeletion = null, RequestOptions? options = null)
        {
            var request = new AppDeleteRequest();
            request.Id = id;
            if (recordDeletion.HasValue)
            {
            request.RecordDeletion = recordDeletion.Value;
            }
            await SendApiAsync("DeleteApp", request, Empty.Parser, options);
        }

        public async Task AddAppToClanAsync(long appId, long clanId, RequestOptions? options = null)
        {
            var request = new AppClan();
            request.AppId = appId;
            request.ClanId = clanId;
            await SendApiAsync("AddAppToClan", request, Empty.Parser, options);
        }

        public Task<ListAuditLog> ListAuditLogAsync(long? clanId = null, string? actionLog = null, long? userId = null, string? dateLog = null, RequestOptions? options = null)
        {
            var request = new ListAuditLogRequest();
            if (clanId.HasValue)
            {
            request.ClanId = clanId.Value;
            }
            if (!string.IsNullOrEmpty(actionLog))
            {
            request.ActionLog = actionLog;
            }
            if (userId.HasValue)
            {
            request.UserId = userId.Value;
            }
            if (!string.IsNullOrEmpty(dateLog))
            {
            request.DateLog = dateLog;
            }
            return SendApiAsync("ListAuditLog", request, ListAuditLog.Parser, options);
        }

        public async Task AddUserEventAsync(UserEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddUserEvent", body, Empty.Parser, options);
        }

        public async Task DeleteUserEventAsync(long clanId, long eventId, RequestOptions? options = null)
        {
            var request = new UserEventRequest();
            request.ClanId = clanId;
            request.EventId = eventId;
            await SendApiAsync("DeleteUserEvent", request, Empty.Parser, options);
        }

        public async Task HealthcheckAsync(RequestOptions? options = null)
        {
            await SendApiAsync("Healthcheck", new Empty(), Empty.Parser, options);
        }

        public Task<ChannelDescList> ListChannelDescsAsync(long clanId, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
        {
            var request = new ListChannelDescsRequest();
            request.ClanId = clanId;
            if (limit.HasValue)
            {
            request.Limit = limit.Value;
            }
            if (state.HasValue)
            {
            request.State = state.Value;
            }
            if (!string.IsNullOrEmpty(cursor))
            {
            request.Cursor = cursor;
            }
            return SendApiAsync("ListChannelDescs", request, ChannelDescList.Parser, options);
        }

        public Task<Internal.Api.ChannelDescription> GetChannelDetailAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ListChannelDetailRequest();
            request.ChannelId = channelId;
            return SendApiAsync("ListChannelDetail", request, Internal.Api.ChannelDescription.Parser, options);
        }

        public Task<BannedUserList> ListBannedUsersAsync(long clanId, RequestOptions? options = null)
        {
            var request = new BannedUserListRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListBannedUsers", request, BannedUserList.Parser, options);
        }

        public async Task UnbanClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            var request = new BanClanUsersRequest();
            request.ClanId = clanId;
            foreach (var userId in userIds)
            {
            request.UserIds.Add(userId);
            }
            await SendApiAsync("UnbanClanUsers", request, Empty.Parser, options);
        }

        public Task<RegistFcmDeviceTokenResponse> RegistFCMDeviceTokenAsync(RegistFcmDeviceTokenRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("RegistFCMDeviceToken", body, RegistFcmDeviceTokenResponse.Parser, options);
        }

        public Task<AllUserClans> ListUserClansByUserIdAsync(RequestOptions? options = null)
        {
            return SendApiAsync("ListUserClansByUserId", new Empty(), AllUserClans.Parser, options);
        }

        public Task<ListChannelAppsResponse> ListChannelAppsAsync(long? clanId = null, RequestOptions? options = null)
        {
            var request = new ListChannelAppsRequest();
            if (clanId.HasValue)
            {
            request.ClanId = clanId.Value;
            }
            return SendApiAsync("ListChannelApps", request, ListChannelAppsResponse.Parser, options);
        }

        public async Task CloseDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;
            await SendApiAsync("CloseDMByChannelId", request, Empty.Parser, options);
        }

        public async Task OpenDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;
            await SendApiAsync("OpenDMByChannelId", request, Empty.Parser, options);
        }

        public Task<ClanProfile> GetUserProfileOnClanAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ClanProfileRequest();
            request.ClanId = clanId;
            return SendApiAsync("GetUserProfileOnClan", request, ClanProfile.Parser, options);
        }

        public async Task UpdateUserProfileByClanAsync(UpdateClanProfileRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateUserProfileByClan", body, Empty.Parser, options);
        }

        public async Task LeaveThreadAsync(long channelId, RequestOptions? options = null)
        {
            var request = new LeaveThreadRequest();
            request.ChannelId = channelId;
            await SendApiAsync("LeaveThread", request, Empty.Parser, options);
        }

        public Task<ChannelDescListNoPool> ListThreadDescsAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ListThreadRequest();
            request.ChannelId = channelId;
            return SendApiAsync("ListThreadDescs", request, ChannelDescListNoPool.Parser, options);
        }

        public Task<ChannelDescList> SearchThreadAsync(SearchThreadRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("SearchThread", body, ChannelDescList.Parser, options);
        }

        public Task<LinkAccountConfirmRequest> LinkSMSAsync(AccountMezon body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("LinkSMS", body, LinkAccountConfirmRequest.Parser, options);
        }

        public async Task ConfirmLinkMezonOTPAsync(LinkAccountConfirmRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ConfirmLinkMezonOTP", body, Empty.Parser, options);
        }

        public Task<LinkAccountConfirmRequest> LinkEmailAsync(AccountEmail body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("LinkEmail", body, LinkAccountConfirmRequest.Parser, options);
        }

        public async Task UnlinkMezonAsync(AccountMezon body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UnlinkMezon", body, Empty.Parser, options);
        }

        public async Task UnlinkEmailAsync(AccountEmail body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UnlinkEmail", body, Empty.Parser, options);
        }

        public Task<IsBannedResponse> IsBannedAsync(long channelId, RequestOptions? options = null)
        {
            var request = new IsBannedRequest();
            request.ChannelId = channelId;
            return SendApiAsync("IsBanned", request, IsBannedResponse.Parser, options);
        }

        public async Task AddRolesChannelDescAsync(AddRoleChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddRolesChannelDesc", body, Empty.Parser, options);
        }

        public async Task DeleteRoleChannelDescAsync(long roleId, RequestOptions? options = null)
        {
            var request = new DeleteRoleRequest();
            request.RoleId = roleId;
            await SendApiAsync("DeleteRoleChannelDesc", request, Empty.Parser, options);
        }

        public async Task SetRoleChannelPermissionAsync(UpdateRoleChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetRoleChannelPermission", body, Empty.Parser, options);
        }

        public Task<RoleList> GetRoleOfUserInTheClanAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListPermissionOfUsersRequest();
            request.ClanId = clanId;
            return SendApiAsync("GetRoleOfUserInTheClan", request, RoleList.Parser, options);
        }

        public Task<PermissionRoleChannelListEventResponse> GetPermissionByRoleIdChannelIdAsync(PermissionRoleChannelListEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetPermissionByRoleIdChannelId", body, PermissionRoleChannelListEventResponse.Parser, options);
        }

        public Task<ChannelAttachmentList> ListChannelAttachmentAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ListChannelAttachmentRequest();
            request.ChannelId = channelId;
            return SendApiAsync("ListChannelAttachment", request, ChannelAttachmentList.Parser, options);
        }

        public Task<VoiceChannelUserList> ListChannelVoiceUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            var request = new ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;
            return SendApiAsync("ListChannelVoiceUsers", request, VoiceChannelUserList.Parser, options);
        }

        public Task<StreamingChannelUserList> ListStreamingChannelUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            var request = new ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;
            return SendApiAsync("ListStreamingChannelUsers", request, StreamingChannelUserList.Parser, options);
        }

        public Task<ChannelDescListNoPool> ListChannelByUserIdAsync(RequestOptions? options = null)
        {
            return SendApiAsync("ListChannelByUserId", new Empty(), ChannelDescListNoPool.Parser, options);
        }

        public Task<NotificationUserChannel> GetNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetNotificationChannel", body, NotificationUserChannel.Parser, options);
        }

        public Task<NotificationUserChannel> GetNotificationCategoryAsync(DefaultNotificationCategory body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetNotificationCategory", body, NotificationUserChannel.Parser, options);
        }

        public async Task SetNotificationCategorySettingAsync(SetNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("SetNotificationCategorySetting", body, Empty.Parser, options);
        }

        public async Task DeleteNotificationCategorySettingAsync(DefaultNotificationCategory body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteNotificationCategorySetting", body, Empty.Parser, options);
        }

        public async Task DeleteNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteNotificationChannel", body, Empty.Parser, options);
        }

        public Task<ChannelMessage> CreateMessage2InboxAsync(Message2InboxRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateMessage2Inbox", body, ChannelMessage.Parser, options);
        }

        public Task<ChannelSettingListResponse> ListChannelSettingAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ChannelSettingListRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListChannelSetting", request, ChannelSettingListResponse.Parser, options);
        }

        public async Task UpdateChannelPrivateAsync(ChangeChannelPrivateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateChannelPrivate", body, Empty.Parser, options);
        }

        public async Task ChangeChannelCategoryAsync(ChangeChannelCategoryRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ChangeChannelCategory", body, Empty.Parser, options);
        }

        public Task<EmojiRecentList> EmojiRecentListAsync(RequestOptions? options = null)
        {
            return SendApiAsync("EmojiRecentList", new Empty(), EmojiRecentList.Parser, options);
        }

        public Task<AllUsersAddChannelResponse> ListChannelUsersUCAsync(AllUsersAddChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("ListChannelUsersUC", body, AllUsersAddChannelResponse.Parser, options);
        }

        public Task<EditChannelCanvasResponse> EditChannelCanvasesAsync(EditChannelCanvasRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("EditChannelCanvases", body, EditChannelCanvasResponse.Parser, options);
        }

        public Task<ChannelCanvasListResponse> GetChannelCanvasListAsync(long channelId, RequestOptions? options = null)
        {
            var request = new ChannelCanvasListRequest();
            request.ChannelId = channelId;
            return SendApiAsync("GetChannelCanvasList", request, ChannelCanvasListResponse.Parser, options);
        }

        public Task<ChannelCanvasDetailResponse> GetChannelCanvasDetailAsync(long id, RequestOptions? options = null)
        {
            var request = new ChannelCanvasDetailRequest();
            request.Id = id;
            return SendApiAsync("GetChannelCanvasDetail", request, ChannelCanvasDetailResponse.Parser, options);
        }

        public async Task DeleteChannelCanvasAsync(long canvasId, RequestOptions? options = null)
        {
            var request = new DeleteChannelCanvasRequest();
            request.CanvasId = canvasId;
            await SendApiAsync("DeleteChannelCanvas", request, Empty.Parser, options);
        }

        public Task<ListFavoriteChannelResponse> GetListFavoriteChannelAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListFavoriteChannelRequest();
            request.ClanId = clanId;
            return SendApiAsync("GetListFavoriteChannel", request, ListFavoriteChannelResponse.Parser, options);
        }

        public Task<AddFavoriteChannelResponse> AddChannelFavoriteAsync(AddFavoriteChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("AddChannelFavorite", body, AddFavoriteChannelResponse.Parser, options);
        }

        public async Task RemoveChannelFavoriteAsync(long channelId, RequestOptions? options = null)
        {
            var request = new RemoveFavoriteChannelRequest();
            request.ChannelId = channelId;
            await SendApiAsync("RemoveChannelFavorite", request, Empty.Parser, options);
        }

        public Task<GenerateClanWebhookResponse> GenerateClanWebhookAsync(GenerateClanWebhookRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GenerateClanWebhook", body, GenerateClanWebhookResponse.Parser, options);
        }

        public Task<ListClanWebhookResponse> ListClanWebhookAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListClanWebhookRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListClanWebhook", request, ListClanWebhookResponse.Parser, options);
        }

        public async Task UpdateClanWebhookByIdAsync(UpdateClanWebhookRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateClanWebhookById", body, Empty.Parser, options);
        }

        public async Task DeleteClanWebhookByIdAsync(long id, RequestOptions? options = null)
        {
            var request = new ClanWebhookRequest();
            request.Id = id;
            await SendApiAsync("DeleteClanWebhookById", request, Empty.Parser, options);
        }

        public Task<ListOnboardingStepResponse> ListOnboardingStepAsync(long clanId, RequestOptions? options = null)
        {
            var request = new ListOnboardingStepRequest();
            request.ClanId = clanId;
            return SendApiAsync("ListOnboardingStep", request, ListOnboardingStepResponse.Parser, options);
        }

        public async Task UpdateOnboardingStepAsync(UpdateOnboardingStepRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateOnboardingStep", body, Empty.Parser, options);
        }

        public async Task DeleteQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteQuickMenuAccess", body, Empty.Parser, options);
        }

        public async Task AddQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddQuickMenuAccess", body, Empty.Parser, options);
        }

        public async Task UpdateQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateQuickMenuAccess", body, Empty.Parser, options);
        }

        public Task<QuickMenuAccessList> ListQuickMenuAccessAsync(long botId, long channelId, int? menuType = null, RequestOptions? options = null)
        {
            var request = new ListQuickMenuAccessRequest();
            request.BotId = botId;
            request.ChannelId = channelId;
            if (menuType.HasValue)
            {
            request.MenuType = menuType.Value;
            }
            return SendApiAsync("ListQuickMenuAccess", request, QuickMenuAccessList.Parser, options);
        }

        public Task<IsFollowerResponse> IsFollowerAsync(IsFollowerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("IsFollower", body, IsFollowerResponse.Parser, options);
        }

        public Task<ChannelMessageAck> SendChannelMessageAsync(ChannelMessageSend body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("SendChannelMessage", body, ChannelMessageAck.Parser, options);
        }

        public async Task UpdateChannelMessageAsync(ChannelMessageUpdate body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("UpdateChannelMessage", body, Empty.Parser, options);
        }

        public async Task DeleteChannelMessageAsync(ChannelMessageRemove body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteChannelMessage", body, Empty.Parser, options);
        }

        public async Task RemoveParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("RemoveParticipantMezonMeet", body, Empty.Parser, options);
        }

        public async Task MuteParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("MuteParticipantMezonMeet", body, Empty.Parser, options);
        }

        public Task<CreateRoomChannelApps> CreateRoomChannelAppsAsync(CreateRoomChannelApps body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateRoomChannelApps", body, CreateRoomChannelApps.Parser, options);
        }

        public Task<GenerateHashChannelAppsResponse> GenerateHashChannelAppsAsync(GenerateHashChannelAppsRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GenerateHashChannelApps", body, GenerateHashChannelAppsResponse.Parser, options);
        }

        public Task<MezonOauthClient> GetMezonOauthClientAsync(GetMezonOauthClientRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetMezonOauthClient", body, MezonOauthClient.Parser, options);
        }

        public async Task DeleteMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteMezonOauthClient", body, Empty.Parser, options);
        }

        public Task<MezonOauthClient> UpdateMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("UpdateMezonOauthClient", body, MezonOauthClient.Parser, options);
        }

        public Task<SdTopicList> ListSdTopicAsync(ListSdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("ListSdTopic", body, SdTopicList.Parser, options);
        }

        public Task<SdTopic> GetTopicDetailAsync(SdTopicDetailRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("GetTopicDetail", body, SdTopic.Parser, options);
        }

        public Task<SdTopic> CreateSdTopicAsync(SdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("CreateSdTopic", body, SdTopic.Parser, options);
        }

        public async Task DeleteSdTopicAsync(DeleteSdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DeleteSdTopic", body, Empty.Parser, options);
        }

        public async Task MessageButtonClickAsync(MessageButtonClicked body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("MessageButtonClick", body, Empty.Parser, options);
        }

        public async Task DropdownBoxSelectedAsync(DropdownBoxSelected body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DropdownBoxSelected", body, Empty.Parser, options);
        }

        public async Task ActiveArchivedThreadAsync(ActiveArchivedThread body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ActiveArchivedThread", body, Empty.Parser, options);
        }

        public async Task AddAgentToChannelAsync(UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("AddAgentToChannel", body, Empty.Parser, options);
        }

        public async Task DisconnectAgentAsync(UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("DisconnectAgent", body, Empty.Parser, options);
        }

        public async Task ReportMessageAbuseAsync(ReportMessageAbuseReqest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("ReportMessageAbuse", body, Empty.Parser, options);
        }

        public Task<StreamHttpCallbackResponse> StreamingServerCallbackAsync(StreamHttpCallbackRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("StreamingServerCallback", body, StreamHttpCallbackResponse.Parser, options);
        }

        public Task<ForSaleItemList> ListForSaleItemsAsync(ListForSaleItemsRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            return SendApiAsync("ListForSaleItems", body, ForSaleItemList.Parser, options);
        }

        public async Task HandleClanWebhookAsync(ClanWebhookHandlerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            await SendApiAsync("HandleClanWebhook", body, Empty.Parser, options);
        }

    }
}