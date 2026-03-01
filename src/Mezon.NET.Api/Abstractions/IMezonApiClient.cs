using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mezon.NET.Api;
using Mezon.NET.Core;
using Mezon.NET.Queue;
using Mezon.Protobuf.Api;

namespace Mezon.NET.Abstractions
{
    public interface IMezonApiClient : IDisposable, IAsyncDisposable
    {
        event Func<string, string, double, Task> ApiSentRequestEvent;

        LoginState LoginState { get; }

        internal MezonRequestQueue RequestQueue { get; }

        Task LoginAsync(TokenType tokenType, string token, RequestOptions? options = null);

        Task LogoutAsync();

        long? CurrentUserId { get; }

        internal TokenType TokenType { get; }

        internal string AuthToken { get; }

        Task SendNoResAsync(string method, string endpoint, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task SendJsonNoResAsync(string method, string endpoint, object payload, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task SendMultipartNoResAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task<Stream> SendAsync(string method, string endpoint, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task<Stream> SendJsonAsync(string method, string endpoint, object payload, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task<Stream> SendMultipartAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        void ConfigureGatewayBasePath(string gatewayBasePath);

        void ConfigureApiBasePath(string apiBasePath);

        // Account management
        Task DeleteAccountAsync();
        Task<Account> GetAccountAsync();
        //// Authentication
        Task<AuthenticationResponse> CheckLoginRequestAsync(string basicAuthUsername, string basicAuthPassword, Api.ConfirmLoginRequest body, RequestOptions? options = null);
        Task ConfirmLoginAsync(Api.ConfirmLoginRequest body, RequestOptions options);
        Task<Api.LoginIDResponse> CreateQRLoginAsync(string basicAuthUsername, string basicAuthPassword, LoginIDRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> AuthenticateEmailAsync(string basicAuthUsername, string basicAuthPassword, EmailAuthenticationRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> AuthenticateMezonAsync(string basicAuthUsername, string basicAuthPassword, AccountMezonRequest body, AccountMezonParams args, RequestOptions? options = null);
        Task<AccountConfirmResponse> AuthenticateSMSOTPAsync(string basicAuthUsername, string basicAuthPassword, AuthenticateSMSRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> RefreshSessionAsync(string basicAuthUsername, string basicAuthPassword, Api.SessionRefreshRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> AuthenticateAppAsync(string basicAuthUsername, string basicAuthPassword, AppAuthenticationRequest body, RequestOptions? options = null);
        Task<bool> AuthenticateAppLogoutAsync(AppAuthenticationLogoutRequest body, RequestOptions? options = null);

        #region Friends
        Task<Mezon.Protobuf.Api.AddFriendsResponse> AddFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        Task BlockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        Task UnblockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        Task DeleteFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.FriendList> ListFriendsAsync(int? state = null, int? limit = null, string? cursor = null, RequestOptions? options = null);
        #endregion

        #region Clan
        Task<ClanDescList> ListClanDescsAsync(PaginationParams args, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ClanDesc> CreateClanDescAsync(string clanName, string? logo = null, string? banner = null, RequestOptions? options = null);
        Task DeleteClanDescAsync(long clanId, RequestOptions? options = null);
        Task UpdateClanDescAsync(Mezon.Protobuf.Api.UpdateClanDescRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ClanUserList> ListClanUsersAsync(long clanId, RequestOptions? options = null);
        Task RemoveClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null);
        Task BanClanUsersAsync(long clanId, long channelId, IEnumerable<long> userIds, int? banTime = null, string? reason = null, RequestOptions? options = null);
        //Task<ClanDescriptionProfileResponse> GetClanDescriptionProfileAsync(string clanId);
        //Task UpdateClanDescriptionProfileAsync(string clanId, object body, RequestOptions? options = null);
        //Task<CheckDuplicateClanNameResponse> CheckDuplicateClanNameAsync(string clanName);
        #endregion

        #region Channel
        Task<Mezon.Protobuf.Api.ChannelDescription> CreateChannelDescAsync(Mezon.Protobuf.Api.CreateChannelDescRequest body, RequestOptions? options = null);
        Task DeleteChannelDescAsync(long channelId, RequestOptions? options = null);
        Task UpdateChannelDescAsync(Mezon.Protobuf.Api.UpdateChannelDescRequest body, RequestOptions? options = null);
        Task AddChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null);
        Task RemoveChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ChannelMessageList> ListChannelMessagesAsync(long clanId, long channelId, long? messageId = null, int? direction = null, int? limit = null, long? topicId = null, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ChannelUserList> ListChannelUsersAsync(long clanId, long channelId, int channelType, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null);
        //Task<ChannelAppsResponse> GetChannelAppsAsync(string? clanId = null, CancellationToken cancellationToken = default);
        #endregion

        #region Users
        Task UpdateUserAsync(Mezon.Protobuf.Api.UpdateUsersRequest body, RequestOptions? options = null);
        //Task<UsersResponse> GetUsersAsync(IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null);
        //Task UpdateUserStatusAsync(UpdateUserStatusRequest body, RequestOptions? options = null);
        //Task<UserStatusResponse> GetUserStatusAsync(string bearerToken);
        #endregion

        #region Roles
        Task<Mezon.Protobuf.Api.Role> CreateRoleAsync(Mezon.Protobuf.Api.CreateRoleRequest body, RequestOptions? options = null);
        Task DeleteRoleAsync(long roleId, RequestOptions? options = null);
        Task UpdateRoleAsync(Mezon.Protobuf.Api.UpdateRoleRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.RoleListEventResponse> ListRolesAsync(long? clanId = null, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null);
        #endregion

        #region Events
        Task<Mezon.Protobuf.Api.EventManagement> CreateEventAsync(Mezon.Protobuf.Api.CreateEventRequest body, RequestOptions? options = null);
        Task DeleteEventAsync(long eventId, RequestOptions? options = null);
        Task UpdateEventAsync(Mezon.Protobuf.Api.UpdateEventRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.EventList> ListEventsAsync(long? clanId = null, RequestOptions? options = null);
        #endregion
        #region Messages (Advanced)
        Task<Mezon.Protobuf.Api.SearchMessageResponse> SearchMessageAsync(Mezon.Protobuf.Api.SearchMessageRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ChannelMessage> CreatePinMessageAsync(Mezon.Protobuf.Api.PinMessageRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.PinMessagesList> GetPinMessagesListAsync(long channelId, long clanId, RequestOptions? options = null);
        Task DeletePinMessageAsync(long messageId, long channelId, long clanId, RequestOptions? options = null);
        Task MarkAsReadAsync(Mezon.Protobuf.Api.MarkAsReadRequest body, RequestOptions? options = null);
        #endregion

        #region Emoji & Stickers
        Task CreateClanEmojiAsync(Mezon.Protobuf.Api.ClanEmojiCreateRequest body, RequestOptions? options = null);
        Task UpdateClanEmojiByIdAsync(Mezon.Protobuf.Api.ClanEmojiUpdateRequest body, RequestOptions? options = null);
        Task DeleteClanEmojiByIdAsync(long emojiId, long clanId, RequestOptions? options = null);
        Task AddClanStickerAsync(Mezon.Protobuf.Api.ClanStickerAddRequest body, RequestOptions? options = null);
        Task UpdateClanStickerByIdAsync(Mezon.Protobuf.Api.ClanStickerUpdateByIdRequest body, RequestOptions? options = null);
        Task DeleteClanStickerByIdAsync(long stickerId, long clanId, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.EmojiListedResponse> GetListEmojisByUserIdAsync(RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.StickerListedResponse> GetListStickersByUserIdAsync(RequestOptions? options = null);
        #endregion

        #region Webhooks
        Task<Mezon.Protobuf.Api.WebhookGenerateResponse> GenerateWebhookAsync(Mezon.Protobuf.Api.WebhookCreateRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.WebhookListResponse> ListWebhookByChannelIdAsync(long channelId, long clanId, RequestOptions? options = null);
        Task UpdateWebhookByIdAsync(Mezon.Protobuf.Api.WebhookUpdateRequestById body, RequestOptions? options = null);
        Task DeleteWebhookByIdAsync(Mezon.Protobuf.Api.WebhookDeleteRequestById body, RequestOptions? options = null);
        #endregion

        #region System Messages
        Task CreateSystemMessageAsync(Mezon.Protobuf.Api.SystemMessageRequest body, RequestOptions? options = null);
        Task UpdateSystemMessageAsync(Mezon.Protobuf.Api.SystemMessageRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.SystemMessage> GetSystemMessageByClanIdAsync(long clanId, RequestOptions? options = null);
        Task DeleteSystemMessageAsync(long clanId, RequestOptions? options = null);
        #endregion

        #region Ordering
        Task UpdateRoleOrderAsync(Mezon.Protobuf.Api.UpdateRoleOrderRequest body, RequestOptions? options = null);
        Task UpdateClanOrderAsync(Mezon.Protobuf.Api.UpdateClanOrderRequest body, RequestOptions? options = null);
        #endregion

        #region Encryption
        Task<Mezon.Protobuf.Api.ChanEncryptionMethod> GetChanEncryptionMethodAsync(long channelId, RequestOptions? options = null);
        Task SetChanEncryptionMethodAsync(Mezon.Protobuf.Api.ChanEncryptionMethod body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.GetPubKeysResponse> GetPublicKeysAsync(IEnumerable<long> userIds, RequestOptions? options = null);
        Task PushPublicKeyAsync(Mezon.Protobuf.Api.PushPubKeyRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.GetKeyServerResp> GetKeyServerAsync(RequestOptions? options = null);
        #endregion

        #region Onboarding
        Task<Mezon.Protobuf.Api.ListOnboardingResponse> ListOnboardingAsync(long clanId, int? guideType = null, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.OnboardingItem> GetOnboardingDetailAsync(long id, long clanId, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ListOnboardingResponse> CreateOnboardingAsync(Mezon.Protobuf.Api.CreateOnboardingRequest body, RequestOptions? options = null);
        Task UpdateOnboardingAsync(Mezon.Protobuf.Api.UpdateOnboardingRequest body, RequestOptions? options = null);
        Task DeleteOnboardingAsync(long id, long clanId, RequestOptions? options = null);
        #endregion

        #region Activity
        Task<Mezon.Protobuf.Api.ListUserActivity> ListActivityAsync(RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.UserActivity> CreateActivityAsync(Mezon.Protobuf.Api.CreateActivityRequest body, RequestOptions? options = null);
        #endregion

        #region Mezon Meet
        Task<Mezon.Protobuf.Api.GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.GenerateMeetTokenResponse> GenerateMeetTokenAsync(Mezon.Protobuf.Api.GenerateMeetTokenRequest body, RequestOptions? options = null);
        #endregion

        #region Ownership
        Task TransferOwnershipAsync(Mezon.Protobuf.Api.TransferOwnershipRequest body, RequestOptions? options = null);
        #endregion

        #region Permissions
        Task<Mezon.Protobuf.Api.PermissionList> GetListPermissionAsync(RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.PermissionList> ListRolePermissionsAsync(long roleId, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.RoleUserList> ListRoleUsersAsync(long roleId, int? limit = null, string? cursor = null, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.UserPermissionInChannelListResponse> ListUserPermissionInChannelAsync(long clanId, long channelId, RequestOptions? options = null);
        #endregion

        #region Notifications
        Task DeleteNotificationsAsync(IEnumerable<long>? ids = null, int? category = null, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.NotificationList> ListNotificationsAsync(long? clanId = null, long? notificationId = null, int? limit = null, int? direction = null, RequestOptions? options = null);
        #endregion

        #region Category
        Task<Mezon.Protobuf.Api.CategoryDesc> CreateCategoryDescAsync(Mezon.Protobuf.Api.CreateCategoryDescRequest body, RequestOptions? options = null);
        Task DeleteCategoryDescAsync(long categoryId, long clanId, RequestOptions? options = null);
        Task UpdateCategoryAsync(Mezon.Protobuf.Api.UpdateCategoryDescRequest body, RequestOptions? options = null);
        Task UpdateCategoryOrderAsync(Mezon.Protobuf.Api.UpdateCategoryOrderRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.CategoryDescList> ListCategoryDescsAsync(long clanId, RequestOptions? options = null);
        #endregion

        #region Invites
        Task<Mezon.Protobuf.Api.LinkInviteUser> CreateLinkInviteUserAsync(Mezon.Protobuf.Api.LinkInviteUserRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.InviteUserRes> InviteUserAsync(long inviteId, RequestOptions? options = null);
        #endregion

        #region Notification Settings
        Task SetNotificationClanSettingAsync(Mezon.Protobuf.Api.SetDefaultNotificationRequest body, RequestOptions? options = null);
        Task SetNotificationChannelSettingAsync(Mezon.Protobuf.Api.SetNotificationRequest body, RequestOptions? options = null);
        Task SetMuteNotificationCategoryAsync(Mezon.Protobuf.Api.SetMuteRequest body, RequestOptions? options = null);
        Task SetMuteNotificationChannelAsync(Mezon.Protobuf.Api.SetMuteRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.NotificationChannelCategorySettingList> GetChannelCategoryNotificationSettingsAsync(long clanId, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.NotificationSetting> GetClanNotificationSettingAsync(long clanId, RequestOptions? options = null);
        #endregion

        #region User Status
        Task<Mezon.Protobuf.Api.UserStatus> GetUserStatusAsync(RequestOptions? options = null);
        Task UpdateUserStatusAsync(Mezon.Protobuf.Api.UserStatusUpdate body, RequestOptions? options = null);
        #endregion

        #region Apps
        Task<Mezon.Protobuf.Api.App> AddAppAsync(Mezon.Protobuf.Api.AddAppRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.AppList> ListAppsAsync(string? filter = null, bool? tombstones = null, string? cursor = null, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.App> GetAppAsync(long id, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.App> UpdateAppAsync(Mezon.Protobuf.Api.UpdateAppRequest body, RequestOptions? options = null);
        Task DeleteAppAsync(long id, bool? recordDeletion = null, RequestOptions? options = null);
        Task AddAppToClanAsync(long appId, long clanId, RequestOptions? options = null);
        #endregion

        #region Audit Log
        Task<Mezon.Protobuf.Api.ListAuditLog> ListAuditLogAsync(long? clanId = null, string? actionLog = null, long? userId = null, string? dateLog = null, RequestOptions? options = null);
        #endregion

        #region Storage
        Task<Mezon.Protobuf.Api.UploadAttachment> UploadAttachmentFileAsync(Mezon.Protobuf.Api.UploadAttachmentRequest body, RequestOptions? options = null);
        #endregion

        #region User Events
        Task AddUserEventAsync(Mezon.Protobuf.Api.UserEventRequest body, RequestOptions? options = null);
        Task DeleteUserEventAsync(long clanId, long eventId, RequestOptions? options = null);
        #endregion

        #region Healthcheck
        Task HealthcheckAsync(RequestOptions? options = null);
        #endregion

        #region Channel Descs
        Task<Mezon.Protobuf.Api.ChannelDescList> ListChannelDescsAsync(long clanId, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ChannelDescription> GetChannelDetailAsync(long channelId, RequestOptions? options = null);
        #endregion

        #region Banned Users
        Task<Mezon.Protobuf.Api.BannedUserList> ListBannedUsersAsync(long clanId, RequestOptions? options = null);
        Task UnbanClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null);
        #endregion

        #region FCM Device Token
        Task<Mezon.Protobuf.Api.RegistFcmDeviceTokenResponse> RegistFCMDeviceTokenAsync(Mezon.Protobuf.Api.RegistFcmDeviceTokenRequest body, RequestOptions? options = null);
        #endregion

        #region User Clans
        Task<Mezon.Protobuf.Api.AllUserClans> ListUserClansByUserIdAsync(RequestOptions? options = null);
        #endregion

        #region Channel Apps
        Task<Mezon.Protobuf.Api.ListChannelAppsResponse> ListChannelAppsAsync(long? clanId = null, RequestOptions? options = null);
        #endregion

        #region Direct Messages
        Task CloseDMByChannelIdAsync(long channelId, RequestOptions? options = null);
        Task OpenDMByChannelIdAsync(long channelId, RequestOptions? options = null);
        #endregion

        #region User Profile
        Task<Mezon.Protobuf.Api.ClanProfile> GetUserProfileOnClanAsync(long clanId, RequestOptions? options = null);
        Task UpdateUserProfileByClanAsync(Mezon.Protobuf.Api.UpdateClanProfileRequest body, RequestOptions? options = null);
        #endregion

        #region Thread
        Task LeaveThreadAsync(long channelId, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ChannelDescListNoPool> ListThreadDescsAsync(long channelId, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ChannelDescList> SearchThreadAsync(Mezon.Protobuf.Api.SearchThreadRequest body, RequestOptions? options = null);
        #endregion

        #region Account Linking
        Task<Mezon.Protobuf.Api.LinkAccountConfirmRequest> LinkSMSAsync(Mezon.Protobuf.Api.AccountMezon body, RequestOptions? options = null);
        Task ConfirmLinkMezonOTPAsync(Mezon.Protobuf.Api.LinkAccountConfirmRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.LinkAccountConfirmRequest> LinkEmailAsync(Mezon.Protobuf.Api.AccountEmail body, RequestOptions? options = null);
        Task UnlinkMezonAsync(Mezon.Protobuf.Api.AccountMezon body, RequestOptions? options = null);
        Task UnlinkEmailAsync(Mezon.Protobuf.Api.AccountEmail body, RequestOptions? options = null);
        #endregion

        #region Banned Check
        Task<Mezon.Protobuf.Api.IsBannedResponse> IsBannedAsync(long channelId, RequestOptions? options = null);
        #endregion

        #region Role Channel Permission
        Task AddRolesChannelDescAsync(Mezon.Protobuf.Api.AddRoleChannelDescRequest body, RequestOptions? options = null);
        Task DeleteRoleChannelDescAsync(long roleId, RequestOptions? options = null);
        Task SetRoleChannelPermissionAsync(Mezon.Protobuf.Api.UpdateRoleChannelRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.RoleList> GetRoleOfUserInTheClanAsync(long clanId, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.PermissionRoleChannelListEventResponse> GetPermissionByRoleIdChannelIdAsync(Mezon.Protobuf.Api.PermissionRoleChannelListEventRequest body, RequestOptions? options = null);
        #endregion

        #region Channel Attachments
        Task<Mezon.Protobuf.Api.ChannelAttachmentList> ListChannelAttachmentAsync(long channelId, RequestOptions? options = null);
        #endregion

        #region Voice Channel Users
        Task<Mezon.Protobuf.Api.VoiceChannelUserList> ListChannelVoiceUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.StreamingChannelUserList> ListStreamingChannelUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null);
        #endregion

        #region Channel By User
        Task<Mezon.Protobuf.Api.ChannelDescListNoPool> ListChannelByUserIdAsync(RequestOptions? options = null);
        #endregion

        #region Notification Category
        Task<Mezon.Protobuf.Api.NotificationUserChannel> GetNotificationChannelAsync(Mezon.Protobuf.Api.NotificationChannel body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.NotificationUserChannel> GetNotificationCategoryAsync(Mezon.Protobuf.Api.DefaultNotificationCategory body, RequestOptions? options = null);
        Task SetNotificationCategorySettingAsync(Mezon.Protobuf.Api.SetNotificationRequest body, RequestOptions? options = null);
        Task DeleteNotificationCategorySettingAsync(Mezon.Protobuf.Api.DefaultNotificationCategory body, RequestOptions? options = null);
        Task DeleteNotificationChannelAsync(Mezon.Protobuf.Api.NotificationChannel body, RequestOptions? options = null);
        #endregion

        #region Inbox Messages
        Task<Mezon.Protobuf.Api.ChannelMessage> CreateMessage2InboxAsync(Mezon.Protobuf.Api.Message2InboxRequest body, RequestOptions? options = null);
        #endregion

        #region Channel Settings
        Task<Mezon.Protobuf.Api.ChannelSettingListResponse> ListChannelSettingAsync(long clanId, RequestOptions? options = null);
        #endregion

        #region Username
        Task<Mezon.Protobuf.Api.Session> UpdateUsernameAsync(Mezon.Protobuf.Api.UpdateUsernameRequest body, RequestOptions? options = null);
        #endregion

        #region Channel Private
        Task UpdateChannelPrivateAsync(Mezon.Protobuf.Api.ChangeChannelPrivateRequest body, RequestOptions? options = null);
        #endregion

        #region Channel Category
        Task ChangeChannelCategoryAsync(Mezon.Protobuf.Api.ChangeChannelCategoryRequest body, RequestOptions? options = null);
        #endregion

        #region Emoji Recent
        Task<Mezon.Protobuf.Api.EmojiRecentList> EmojiRecentListAsync(RequestOptions? options = null);
        #endregion

        #region Channel Users UC
        Task<Mezon.Protobuf.Api.AllUsersAddChannelResponse> ListChannelUsersUCAsync(Mezon.Protobuf.Api.AllUsersAddChannelRequest body, RequestOptions? options = null);
        #endregion

        #region Channel Canvas
        Task<Mezon.Protobuf.Api.EditChannelCanvasResponse> EditChannelCanvasesAsync(Mezon.Protobuf.Api.EditChannelCanvasRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ChannelCanvasListResponse> GetChannelCanvasListAsync(long channelId, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ChannelCanvasDetailResponse> GetChannelCanvasDetailAsync(long id, RequestOptions? options = null);
        Task DeleteChannelCanvasAsync(long canvasId, RequestOptions? options = null);
        #endregion

        #region Favorite Channel
        Task<Mezon.Protobuf.Api.ListFavoriteChannelResponse> GetListFavoriteChannelAsync(long clanId, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.AddFavoriteChannelResponse> AddChannelFavoriteAsync(Mezon.Protobuf.Api.AddFavoriteChannelRequest body, RequestOptions? options = null);
        Task RemoveChannelFavoriteAsync(long channelId, RequestOptions? options = null);
        #endregion

        #region Clan Webhook
        Task<Mezon.Protobuf.Api.GenerateClanWebhookResponse> GenerateClanWebhookAsync(Mezon.Protobuf.Api.GenerateClanWebhookRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ListClanWebhookResponse> ListClanWebhookAsync(long clanId, RequestOptions? options = null);
        Task UpdateClanWebhookByIdAsync(Mezon.Protobuf.Api.UpdateClanWebhookRequest body, RequestOptions? options = null);
        Task DeleteClanWebhookByIdAsync(long id, RequestOptions? options = null);
        #endregion

        #region Onboarding Step
        Task<Mezon.Protobuf.Api.ListOnboardingStepResponse> ListOnboardingStepAsync(long clanId, RequestOptions? options = null);
        Task UpdateOnboardingStepAsync(Mezon.Protobuf.Api.UpdateOnboardingStepRequest body, RequestOptions? options = null);
        #endregion

        #region Clan Unread Message Indicator
        Task<Mezon.Protobuf.Api.ListClanUnreadMsgIndicatorResponse> ListClanUnreadMsgIndicatorAsync(long clanId, RequestOptions? options = null);
        #endregion

        #region Quick Menu Access
        Task DeleteQuickMenuAccessAsync(Mezon.Protobuf.Api.QuickMenuAccess body, RequestOptions? options = null);
        Task AddQuickMenuAccessAsync(Mezon.Protobuf.Api.QuickMenuAccess body, RequestOptions? options = null);
        Task UpdateQuickMenuAccessAsync(Mezon.Protobuf.Api.QuickMenuAccess body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.QuickMenuAccessList> ListQuickMenuAccessAsync(long botId, long channelId, int? menuType = null, RequestOptions? options = null);
        #endregion

        #region Follower
        Task<Mezon.Protobuf.Api.IsFollowerResponse> IsFollowerAsync(Mezon.Protobuf.Api.IsFollowerRequest body, RequestOptions? options = null);
        #endregion

        #region Channel Messages
        Task<Mezon.Protobuf.Realtime.ChannelMessageAck> SendChannelMessageAsync(Mezon.Protobuf.Realtime.ChannelMessageSend body, RequestOptions? options = null);
        Task UpdateChannelMessageAsync(Mezon.Protobuf.Realtime.ChannelMessageUpdate body, RequestOptions? options = null);
        Task DeleteChannelMessageAsync(Mezon.Protobuf.Realtime.ChannelMessageRemove body, RequestOptions? options = null);
        #endregion

        #region Mezon Meet Participant
        Task RemoveParticipantMezonMeetAsync(Mezon.Protobuf.Api.MeetParticipantRequest body, RequestOptions? options = null);
        Task MuteParticipantMezonMeetAsync(Mezon.Protobuf.Api.MeetParticipantRequest body, RequestOptions? options = null);
        #endregion

        #region Room Channel Apps
        Task<Mezon.Protobuf.Api.CreateRoomChannelApps> CreateRoomChannelAppsAsync(Mezon.Protobuf.Api.CreateRoomChannelApps body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.GenerateHashChannelAppsResponse> GenerateHashChannelAppsAsync(Mezon.Protobuf.Api.GenerateHashChannelAppsRequest body, RequestOptions? options = null);
        #endregion

        #region OAuth Client
        Task<Mezon.Protobuf.Api.MezonOauthClient> GetMezonOauthClientAsync(Mezon.Protobuf.Api.GetMezonOauthClientRequest body, RequestOptions? options = null);
        Task DeleteMezonOauthClientAsync(Mezon.Protobuf.Api.MezonOauthClient body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.MezonOauthClient> UpdateMezonOauthClientAsync(Mezon.Protobuf.Api.MezonOauthClient body, RequestOptions? options = null);
        #endregion

        #region SD Topics
        Task<Mezon.Protobuf.Api.SdTopicList> ListSdTopicAsync(Mezon.Protobuf.Api.ListSdTopicRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.SdTopic> GetTopicDetailAsync(Mezon.Protobuf.Api.SdTopicDetailRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.SdTopic> CreateSdTopicAsync(Mezon.Protobuf.Api.SdTopicRequest body, RequestOptions? options = null);
        Task DeleteSdTopicAsync(Mezon.Protobuf.Api.DeleteSdTopicRequest body, RequestOptions? options = null);
        #endregion

        #region Interactive
        Task MessageButtonClickAsync(Mezon.Protobuf.Realtime.MessageButtonClicked body, RequestOptions? options = null);
        Task DropdownBoxSelectedAsync(Mezon.Protobuf.Realtime.DropdownBoxSelected body, RequestOptions? options = null);
        #endregion

        #region Voice State
        Task UpdateMezonVoiceStateAsync(Mezon.Protobuf.Realtime.HandleParticipantMeetStateEvent body, RequestOptions? options = null);
        #endregion

        #region Archived Thread
        Task ActiveArchivedThreadAsync(Mezon.Protobuf.Realtime.ActiveArchivedThread body, RequestOptions? options = null);
        #endregion

        #region AI Agent
        Task AddAgentToChannelAsync(Mezon.Protobuf.Api.UpdateAIAgentRequest body, RequestOptions? options = null);
        Task DisconnectAgentAsync(Mezon.Protobuf.Api.UpdateAIAgentRequest body, RequestOptions? options = null);
        #endregion

        #region Report Message
        Task ReportMessageAbuseAsync(Mezon.Protobuf.Api.ReportMessageAbuseReqest body, RequestOptions? options = null);
        #endregion

        #region Registration
        Task<AuthenticationResponse> RegistrationEmailAsync(string basicAuthUsername, string basicAuthPassword, Mezon.Protobuf.Api.RegistrationEmailRequest body, RequestOptions? options = null);
        #endregion

        #region OAuth File Upload
        Task<Mezon.Protobuf.Api.UploadAttachment> UploadOauthFileAsync(Mezon.Protobuf.Api.UploadAttachmentRequest body, RequestOptions? options = null);
        #endregion

        #region Account Update
        Task UpdateAccountAsync(Mezon.Protobuf.Api.UpdateAccountRequest body, RequestOptions? options = null);
        #endregion

        #region Streaming Callback
        Task<Mezon.Protobuf.Api.StreamHttpCallbackResponse> StreamingServerCallbackAsync(Mezon.Protobuf.Api.StreamHttpCallbackRequest body, RequestOptions? options = null);
        #endregion

        #region For Sale Items
        Task<Mezon.Protobuf.Api.ForSaleItemList> ListForSaleItemsAsync(Mezon.Protobuf.Api.ListForSaleItemsRequest body, RequestOptions? options = null);
        #endregion

        #region Clan Webhook Handler
        Task HandleClanWebhookAsync(Mezon.Protobuf.Api.ClanWebhookHandlerRequest body, RequestOptions? options = null);
        #endregion
    }
}
