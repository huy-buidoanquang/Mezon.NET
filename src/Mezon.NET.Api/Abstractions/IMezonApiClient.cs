using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mezon.Net.Api;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Queue;

namespace Mezon.Net.Abstractions
{
    public interface IMezonApiClient : IDisposable, IAsyncDisposable
    {
        event Func<string, string, double, Task> ApiSentRequestEvent;

        LoginState LoginState { get; }

        internal RequestQueue RequestQueue { get; }

        Task LoginAsync(TokenType tokenType, string token, RequestOptions? options = null);

        Task LogoutAsync();

        long? CurrentUserId { get; }

        internal TokenType TokenType { get; }

        internal string AuthToken { get; }

        Task SendNoResAsync(string method, string endpoint, BucketId? bucketId = null, ApiBucketType clientBucket = ApiBucketType.Unbucketed, RequestOptions? options = null);

        Task SendJsonNoResAsync(string method, string endpoint, object payload, BucketId? bucketId = null, ApiBucketType clientBucket = ApiBucketType.Unbucketed, RequestOptions? options = null);

        Task SendMultipartNoResAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, BucketId? bucketId = null, ApiBucketType clientBucket = ApiBucketType.Unbucketed, RequestOptions? options = null);

        Task<System.IO.Stream> SendAsync(string method, string endpoint, BucketId? bucketId = null, ApiBucketType clientBucket = ApiBucketType.Unbucketed, RequestOptions? options = null);

        Task<System.IO.Stream> SendJsonAsync(string method, string endpoint, object payload, BucketId? bucketId = null, ApiBucketType clientBucket = ApiBucketType.Unbucketed, RequestOptions? options = null);

        Task<System.IO.Stream> SendMultipartAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, BucketId? bucketId = null, ApiBucketType clientBucket = ApiBucketType.Unbucketed, RequestOptions? options = null);

        void ConfigureGatewayBasePath(string gatewayBasePath);

        void ConfigureApiBasePath(string apiBasePath);

        //// Account management
        //Task DeleteAccountAsync();
        //Task<Account> GetAccountAsync();
        ////// Authentication
        //Task<AuthenticationResponse> CheckLoginRequestAsync(string basicAuthUsername, string basicAuthPassword, Api.ConfirmLoginRequest body, RequestOptions? options = null);
        //Task ConfirmLoginAsync(Api.ConfirmLoginRequest body, RequestOptions options);
        Task<Api.LoginIDResponse> CreateQRLoginAsync(string basicAuthUsername, string basicAuthPassword, LoginIDRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> AuthenticateEmailAsync(string basicAuthUsername, string basicAuthPassword, EmailAuthenticationRequest body, RequestOptions? options = null);
        //Task<AuthenticationResponse> AuthenticateMezonAsync(string basicAuthUsername, string basicAuthPassword, AccountMezonRequest body, AccountMezonParams args, RequestOptions? options = null);
        //Task<AccountConfirmResponse> AuthenticateSMSOTPAsync(string basicAuthUsername, string basicAuthPassword, AuthenticateSMSRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> RefreshSessionAsync(string basicAuthUsername, string basicAuthPassword, Api.SessionRefreshRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> AuthenticateAppAsync(string basicAuthUsername, string basicAuthPassword, AppAuthenticationRequest body, RequestOptions? options = null);
        Task<bool> AuthenticateAppLogoutAsync(AppAuthenticationLogoutRequest body, RequestOptions? options = null);

        //#region Friends
        //Task<Mezon.Net.Internal.Protos.AddFriendsResponse> AddFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        //Task BlockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        //Task UnblockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        //Task DeleteFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.FriendList> ListFriendsAsync(int? state = null, int? limit = null, string? cursor = null, RequestOptions? options = null);
        //#endregion

        //#region Clan
        Task<ClanDescList> ListClanDescsAsync(PaginationParams args, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ClanDesc> CreateClanDescAsync(string clanName, string? logo = null, string? banner = null, RequestOptions? options = null);
        //Task DeleteClanDescAsync(long clanId, RequestOptions? options = null);
        //Task UpdateClanDescAsync(Mezon.Net.Internal.Protos.UpdateClanDescRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ClanUserList> ListClanUsersAsync(long clanId, RequestOptions? options = null);
        //Task RemoveClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null);
        //Task BanClanUsersAsync(long clanId, long channelId, IEnumerable<long> userIds, int? banTime = null, string? reason = null, RequestOptions? options = null);
        ////Task<ClanDescriptionProfileResponse> GetClanDescriptionProfileAsync(string clanId);
        ////Task UpdateClanDescriptionProfileAsync(string clanId, object body, RequestOptions? options = null);
        ////Task<CheckDuplicateClanNameResponse> CheckDuplicateClanNameAsync(string clanName);
        //#endregion

        //#region Channel
        //Task<Mezon.Net.Internal.Protos.ChannelDescription> CreateChannelDescAsync(Mezon.Net.Internal.Protos.CreateChannelDescRequest body, RequestOptions? options = null);
        //Task DeleteChannelDescAsync(long channelId, RequestOptions? options = null);
        //Task UpdateChannelDescAsync(Mezon.Net.Internal.Protos.UpdateChannelDescRequest body, RequestOptions? options = null);
        //Task AddChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null);
        //Task RemoveChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ChannelMessageList> ListChannelMessagesAsync(long clanId, long channelId, long? messageId = null, int? direction = null, int? limit = null, long? topicId = null, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ChannelUserList> ListChannelUsersAsync(long clanId, long channelId, int channelType, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null);
        ////Task<ChannelAppsResponse> GetChannelAppsAsync(string? clanId = null, CancellationToken cancellationToken = default);
        //#endregion

        //#region Users
        //Task UpdateUserAsync(Mezon.Net.Internal.Protos.UpdateUsersRequest body, RequestOptions? options = null);
        ////Task<UsersResponse> GetUsersAsync(IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null);
        ////Task UpdateUserStatusAsync(UpdateUserStatusRequest body, RequestOptions? options = null);
        ////Task<UserStatusResponse> GetUserStatusAsync(string bearerToken);
        //#endregion

        //#region Roles
        //Task<Mezon.Net.Internal.Protos.Role> CreateRoleAsync(Mezon.Net.Internal.Protos.CreateRoleRequest body, RequestOptions? options = null);
        //Task DeleteRoleAsync(long roleId, RequestOptions? options = null);
        //Task UpdateRoleAsync(Mezon.Net.Internal.Protos.UpdateRoleRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.RoleListEventResponse> ListRolesAsync(long? clanId = null, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null);
        //#endregion

        //#region Events
        //Task<Mezon.Net.Internal.Protos.EventManagement> CreateEventAsync(Mezon.Net.Internal.Protos.CreateEventRequest body, RequestOptions? options = null);
        //Task DeleteEventAsync(long eventId, RequestOptions? options = null);
        //Task UpdateEventAsync(Mezon.Net.Internal.Protos.UpdateEventRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.EventList> ListEventsAsync(long? clanId = null, RequestOptions? options = null);
        //#endregion
        //#region Messages (Advanced)
        //Task<Mezon.Net.Internal.Protos.SearchMessageResponse> SearchMessageAsync(Mezon.Net.Internal.Protos.SearchMessageRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ChannelMessage> CreatePinMessageAsync(Mezon.Net.Internal.Protos.PinMessageRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.PinMessagesList> GetPinMessagesListAsync(long channelId, long clanId, RequestOptions? options = null);
        //Task DeletePinMessageAsync(long messageId, long channelId, long clanId, RequestOptions? options = null);
        //Task MarkAsReadAsync(Mezon.Net.Internal.Protos.MarkAsReadRequest body, RequestOptions? options = null);
        //#endregion

        //#region Emoji & Stickers
        //Task CreateClanEmojiAsync(Mezon.Net.Internal.Protos.ClanEmojiCreateRequest body, RequestOptions? options = null);
        //Task UpdateClanEmojiByIdAsync(Mezon.Net.Internal.Protos.ClanEmojiUpdateRequest body, RequestOptions? options = null);
        //Task DeleteClanEmojiByIdAsync(long emojiId, long clanId, RequestOptions? options = null);
        //Task AddClanStickerAsync(Mezon.Net.Internal.Protos.ClanStickerAddRequest body, RequestOptions? options = null);
        //Task UpdateClanStickerByIdAsync(Mezon.Net.Internal.Protos.ClanStickerUpdateByIdRequest body, RequestOptions? options = null);
        //Task DeleteClanStickerByIdAsync(long stickerId, long clanId, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.EmojiListedResponse> GetListEmojisByUserIdAsync(RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.StickerListedResponse> GetListStickersByUserIdAsync(RequestOptions? options = null);
        //#endregion

        //#region Webhooks
        //Task<Mezon.Net.Internal.Protos.WebhookGenerateResponse> GenerateWebhookAsync(Mezon.Net.Internal.Protos.WebhookCreateRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.WebhookListResponse> ListWebhookByChannelIdAsync(long channelId, long clanId, RequestOptions? options = null);
        //Task UpdateWebhookByIdAsync(Mezon.Net.Internal.Protos.WebhookUpdateRequestById body, RequestOptions? options = null);
        //Task DeleteWebhookByIdAsync(Mezon.Net.Internal.Protos.WebhookDeleteRequestById body, RequestOptions? options = null);
        //#endregion

        //#region System Messages
        //Task CreateSystemMessageAsync(Mezon.Net.Internal.Protos.SystemMessageRequest body, RequestOptions? options = null);
        //Task UpdateSystemMessageAsync(Mezon.Net.Internal.Protos.SystemMessageRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.SystemMessage> GetSystemMessageByClanIdAsync(long clanId, RequestOptions? options = null);
        //Task DeleteSystemMessageAsync(long clanId, RequestOptions? options = null);
        //#endregion

        //#region Ordering
        //Task UpdateRoleOrderAsync(Mezon.Net.Internal.Protos.UpdateRoleOrderRequest body, RequestOptions? options = null);
        //Task UpdateClanOrderAsync(Mezon.Net.Internal.Protos.UpdateClanOrderRequest body, RequestOptions? options = null);
        //#endregion

        //#region Encryption
        //Task<Mezon.Net.Internal.Protos.ChanEncryptionMethod> GetChanEncryptionMethodAsync(long channelId, RequestOptions? options = null);
        //Task SetChanEncryptionMethodAsync(Mezon.Net.Internal.Protos.ChanEncryptionMethod body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.GetPubKeysResponse> GetPublicKeysAsync(IEnumerable<long> userIds, RequestOptions? options = null);
        //Task PushPublicKeyAsync(Mezon.Net.Internal.Protos.PushPubKeyRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.GetKeyServerResp> GetKeyServerAsync(RequestOptions? options = null);
        //#endregion

        //#region Onboarding
        //Task<Mezon.Net.Internal.Protos.ListOnboardingResponse> ListOnboardingAsync(long clanId, int? guideType = null, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.OnboardingItem> GetOnboardingDetailAsync(long id, long clanId, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ListOnboardingResponse> CreateOnboardingAsync(Mezon.Net.Internal.Protos.CreateOnboardingRequest body, RequestOptions? options = null);
        //Task UpdateOnboardingAsync(Mezon.Net.Internal.Protos.UpdateOnboardingRequest body, RequestOptions? options = null);
        //Task DeleteOnboardingAsync(long id, long clanId, RequestOptions? options = null);
        //#endregion

        //#region Activity
        //Task<Mezon.Net.Internal.Protos.ListUserActivity> ListActivityAsync(RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.UserActivity> CreateActivityAsync(Mezon.Net.Internal.Protos.CreateActivityRequest body, RequestOptions? options = null);
        //#endregion

        //#region Mezon Meet
        //Task<Mezon.Net.Internal.Protos.GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.GenerateMeetTokenResponse> GenerateMeetTokenAsync(Mezon.Net.Internal.Protos.GenerateMeetTokenRequest body, RequestOptions? options = null);
        //#endregion

        //#region Ownership
        //Task TransferOwnershipAsync(Mezon.Net.Internal.Protos.TransferOwnershipRequest body, RequestOptions? options = null);
        //#endregion

        //#region Permissions
        //Task<Mezon.Net.Internal.Protos.PermissionList> GetListPermissionAsync(RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.PermissionList> ListRolePermissionsAsync(long roleId, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.RoleUserList> ListRoleUsersAsync(long roleId, int? limit = null, string? cursor = null, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.UserPermissionInChannelListResponse> ListUserPermissionInChannelAsync(long clanId, long channelId, RequestOptions? options = null);
        //#endregion

        //#region Notifications
        //Task DeleteNotificationsAsync(IEnumerable<long>? ids = null, int? category = null, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.NotificationList> ListNotificationsAsync(long? clanId = null, long? notificationId = null, int? limit = null, int? direction = null, RequestOptions? options = null);
        //#endregion

        //#region Category
        //Task<Mezon.Net.Internal.Protos.CategoryDesc> CreateCategoryDescAsync(Mezon.Net.Internal.Protos.CreateCategoryDescRequest body, RequestOptions? options = null);
        //Task DeleteCategoryDescAsync(long categoryId, long clanId, RequestOptions? options = null);
        //Task UpdateCategoryAsync(Mezon.Net.Internal.Protos.UpdateCategoryDescRequest body, RequestOptions? options = null);
        //Task UpdateCategoryOrderAsync(Mezon.Net.Internal.Protos.UpdateCategoryOrderRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.CategoryDescList> ListCategoryDescsAsync(long clanId, RequestOptions? options = null);
        //#endregion

        //#region Invites
        //Task<Mezon.Net.Internal.Protos.LinkInviteUser> CreateLinkInviteUserAsync(Mezon.Net.Internal.Protos.LinkInviteUserRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.InviteUserRes> InviteUserAsync(long inviteId, RequestOptions? options = null);
        //#endregion

        //#region Notification Settings
        //Task SetNotificationClanSettingAsync(Mezon.Net.Internal.Protos.SetDefaultNotificationRequest body, RequestOptions? options = null);
        //Task SetNotificationChannelSettingAsync(Mezon.Net.Internal.Protos.SetNotificationRequest body, RequestOptions? options = null);
        //Task SetMuteNotificationCategoryAsync(Mezon.Net.Internal.Protos.SetMuteRequest body, RequestOptions? options = null);
        //Task SetMuteNotificationChannelAsync(Mezon.Net.Internal.Protos.SetMuteRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.NotificationChannelCategorySettingList> GetChannelCategoryNotificationSettingsAsync(long clanId, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.NotificationSetting> GetClanNotificationSettingAsync(long clanId, RequestOptions? options = null);
        //#endregion

        //#region User Status
        //Task<Mezon.Net.Internal.Protos.UserStatus> GetUserStatusAsync(RequestOptions? options = null);
        //Task UpdateUserStatusAsync(Mezon.Net.Internal.Protos.UserStatusUpdate body, RequestOptions? options = null);
        //#endregion

        //#region Apps
        //Task<Mezon.Net.Internal.Protos.App> AddAppAsync(Mezon.Net.Internal.Protos.AddAppRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.AppList> ListAppsAsync(string? filter = null, bool? tombstones = null, string? cursor = null, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.App> GetAppAsync(long id, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.App> UpdateAppAsync(Mezon.Net.Internal.Protos.UpdateAppRequest body, RequestOptions? options = null);
        //Task DeleteAppAsync(long id, bool? recordDeletion = null, RequestOptions? options = null);
        //Task AddAppToClanAsync(long appId, long clanId, RequestOptions? options = null);
        //#endregion

        //#region Audit Log
        //Task<Mezon.Net.Internal.Protos.ListAuditLog> ListAuditLogAsync(long? clanId = null, string? actionLog = null, long? userId = null, string? dateLog = null, RequestOptions? options = null);
        //#endregion

        //#region Storage
        //Task<Mezon.Net.Internal.Protos.UploadAttachment> UploadAttachmentFileAsync(Mezon.Net.Internal.Protos.UploadAttachmentRequest body, RequestOptions? options = null);
        //#endregion

        //#region User Events
        //Task AddUserEventAsync(Mezon.Net.Internal.Protos.UserEventRequest body, RequestOptions? options = null);
        //Task DeleteUserEventAsync(long clanId, long eventId, RequestOptions? options = null);
        //#endregion

        //#region Healthcheck
        //Task HealthcheckAsync(RequestOptions? options = null);
        //#endregion

        //#region Channel Descs
        //Task<Mezon.Net.Internal.Protos.ChannelDescList> ListChannelDescsAsync(long clanId, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ChannelDescription> GetChannelDetailAsync(long channelId, RequestOptions? options = null);
        //#endregion

        //#region Banned Users
        //Task<Mezon.Net.Internal.Protos.BannedUserList> ListBannedUsersAsync(long clanId, RequestOptions? options = null);
        //Task UnbanClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null);
        //#endregion

        //#region FCM Device Token
        //Task<Mezon.Net.Internal.Protos.RegistFcmDeviceTokenResponse> RegistFCMDeviceTokenAsync(Mezon.Net.Internal.Protos.RegistFcmDeviceTokenRequest body, RequestOptions? options = null);
        //#endregion

        //#region User Clans
        //Task<Mezon.Net.Internal.Protos.AllUserClans> ListUserClansByUserIdAsync(RequestOptions? options = null);
        //#endregion

        //#region Channel Apps
        //Task<Mezon.Net.Internal.Protos.ListChannelAppsResponse> ListChannelAppsAsync(long? clanId = null, RequestOptions? options = null);
        //#endregion

        //#region Direct Messages
        //Task CloseDMByChannelIdAsync(long channelId, RequestOptions? options = null);
        //Task OpenDMByChannelIdAsync(long channelId, RequestOptions? options = null);
        //#endregion

        //#region User Profile
        //Task<Mezon.Net.Internal.Protos.ClanProfile> GetUserProfileOnClanAsync(long clanId, RequestOptions? options = null);
        //Task UpdateUserProfileByClanAsync(Mezon.Net.Internal.Protos.UpdateClanProfileRequest body, RequestOptions? options = null);
        //#endregion

        //#region Thread
        //Task LeaveThreadAsync(long channelId, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ChannelDescListNoPool> ListThreadDescsAsync(long channelId, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ChannelDescList> SearchThreadAsync(Mezon.Net.Internal.Protos.SearchThreadRequest body, RequestOptions? options = null);
        //#endregion

        //#region Account Linking
        //Task<Mezon.Net.Internal.Protos.LinkAccountConfirmRequest> LinkSMSAsync(Mezon.Net.Internal.Protos.AccountMezon body, RequestOptions? options = null);
        //Task ConfirmLinkMezonOTPAsync(Mezon.Net.Internal.Protos.LinkAccountConfirmRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.LinkAccountConfirmRequest> LinkEmailAsync(Mezon.Net.Internal.Protos.AccountEmail body, RequestOptions? options = null);
        //Task UnlinkMezonAsync(Mezon.Net.Internal.Protos.AccountMezon body, RequestOptions? options = null);
        //Task UnlinkEmailAsync(Mezon.Net.Internal.Protos.AccountEmail body, RequestOptions? options = null);
        //#endregion

        //#region Banned Check
        //Task<Mezon.Net.Internal.Protos.IsBannedResponse> IsBannedAsync(long channelId, RequestOptions? options = null);
        //#endregion

        //#region Role Channel Permission
        //Task AddRolesChannelDescAsync(Mezon.Net.Internal.Protos.AddRoleChannelDescRequest body, RequestOptions? options = null);
        //Task DeleteRoleChannelDescAsync(long roleId, RequestOptions? options = null);
        //Task SetRoleChannelPermissionAsync(Mezon.Net.Internal.Protos.UpdateRoleChannelRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.RoleList> GetRoleOfUserInTheClanAsync(long clanId, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.PermissionRoleChannelListEventResponse> GetPermissionByRoleIdChannelIdAsync(Mezon.Net.Internal.Protos.PermissionRoleChannelListEventRequest body, RequestOptions? options = null);
        //#endregion

        //#region Channel Attachments
        //Task<Mezon.Net.Internal.Protos.ChannelAttachmentList> ListChannelAttachmentAsync(long channelId, RequestOptions? options = null);
        //#endregion

        //#region Voice Channel Users
        //Task<Mezon.Net.Internal.Protos.VoiceChannelUserList> ListChannelVoiceUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.StreamingChannelUserList> ListStreamingChannelUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null);
        //#endregion

        //#region Channel By User
        //Task<Mezon.Net.Internal.Protos.ChannelDescListNoPool> ListChannelByUserIdAsync(RequestOptions? options = null);
        //#endregion

        //#region Notification Category
        //Task<Mezon.Net.Internal.Protos.NotificationUserChannel> GetNotificationChannelAsync(Mezon.Net.Internal.Protos.NotificationChannel body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.NotificationUserChannel> GetNotificationCategoryAsync(Mezon.Net.Internal.Protos.DefaultNotificationCategory body, RequestOptions? options = null);
        //Task SetNotificationCategorySettingAsync(Mezon.Net.Internal.Protos.SetNotificationRequest body, RequestOptions? options = null);
        //Task DeleteNotificationCategorySettingAsync(Mezon.Net.Internal.Protos.DefaultNotificationCategory body, RequestOptions? options = null);
        //Task DeleteNotificationChannelAsync(Mezon.Net.Internal.Protos.NotificationChannel body, RequestOptions? options = null);
        //#endregion

        //#region Inbox Messages
        //Task<Mezon.Net.Internal.Protos.ChannelMessage> CreateMessage2InboxAsync(Mezon.Net.Internal.Protos.Message2InboxRequest body, RequestOptions? options = null);
        //#endregion

        //#region Channel Settings
        //Task<Mezon.Net.Internal.Protos.ChannelSettingListResponse> ListChannelSettingAsync(long clanId, RequestOptions? options = null);
        //#endregion

        //#region Username
        //Task<Mezon.Net.Internal.Protos.Session> UpdateUsernameAsync(Mezon.Net.Internal.Protos.UpdateUsernameRequest body, RequestOptions? options = null);
        //#endregion

        //#region Channel Private
        //Task UpdateChannelPrivateAsync(Mezon.Net.Internal.Protos.ChangeChannelPrivateRequest body, RequestOptions? options = null);
        //#endregion

        //#region Channel Category
        //Task ChangeChannelCategoryAsync(Mezon.Net.Internal.Protos.ChangeChannelCategoryRequest body, RequestOptions? options = null);
        //#endregion

        //#region Emoji Recent
        //Task<Mezon.Net.Internal.Protos.EmojiRecentList> EmojiRecentListAsync(RequestOptions? options = null);
        //#endregion

        //#region Channel Users UC
        //Task<Mezon.Net.Internal.Protos.AllUsersAddChannelResponse> ListChannelUsersUCAsync(Mezon.Net.Internal.Protos.AllUsersAddChannelRequest body, RequestOptions? options = null);
        //#endregion

        //#region Channel Canvas
        //Task<Mezon.Net.Internal.Protos.EditChannelCanvasResponse> EditChannelCanvasesAsync(Mezon.Net.Internal.Protos.EditChannelCanvasRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ChannelCanvasListResponse> GetChannelCanvasListAsync(long channelId, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ChannelCanvasDetailResponse> GetChannelCanvasDetailAsync(long id, RequestOptions? options = null);
        //Task DeleteChannelCanvasAsync(long canvasId, RequestOptions? options = null);
        //#endregion

        //#region Favorite Channel
        //Task<Mezon.Net.Internal.Protos.ListFavoriteChannelResponse> GetListFavoriteChannelAsync(long clanId, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.AddFavoriteChannelResponse> AddChannelFavoriteAsync(Mezon.Net.Internal.Protos.AddFavoriteChannelRequest body, RequestOptions? options = null);
        //Task RemoveChannelFavoriteAsync(long channelId, RequestOptions? options = null);
        //#endregion

        //#region Clan Webhook
        //Task<Mezon.Net.Internal.Protos.GenerateClanWebhookResponse> GenerateClanWebhookAsync(Mezon.Net.Internal.Protos.GenerateClanWebhookRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.ListClanWebhookResponse> ListClanWebhookAsync(long clanId, RequestOptions? options = null);
        //Task UpdateClanWebhookByIdAsync(Mezon.Net.Internal.Protos.UpdateClanWebhookRequest body, RequestOptions? options = null);
        //Task DeleteClanWebhookByIdAsync(long id, RequestOptions? options = null);
        //#endregion

        //#region Onboarding Step
        //Task<Mezon.Net.Internal.Protos.ListOnboardingStepResponse> ListOnboardingStepAsync(long clanId, RequestOptions? options = null);
        //Task UpdateOnboardingStepAsync(Mezon.Net.Internal.Protos.UpdateOnboardingStepRequest body, RequestOptions? options = null);
        //#endregion

        //#region Clan Unread Message Indicator
        //Task<Mezon.Net.Internal.Protos.ListClanUnreadMsgIndicatorResponse> ListClanUnreadMsgIndicatorAsync(long clanId, RequestOptions? options = null);
        //#endregion

        //#region Quick Menu Access
        //Task DeleteQuickMenuAccessAsync(Mezon.Net.Internal.Protos.QuickMenuAccess body, RequestOptions? options = null);
        //Task AddQuickMenuAccessAsync(Mezon.Net.Internal.Protos.QuickMenuAccess body, RequestOptions? options = null);
        //Task UpdateQuickMenuAccessAsync(Mezon.Net.Internal.Protos.QuickMenuAccess body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.QuickMenuAccessList> ListQuickMenuAccessAsync(long botId, long channelId, int? menuType = null, RequestOptions? options = null);
        //#endregion

        //#region Follower
        //Task<Mezon.Net.Internal.Protos.IsFollowerResponse> IsFollowerAsync(Mezon.Net.Internal.Protos.IsFollowerRequest body, RequestOptions? options = null);
        //#endregion

        //#region Channel Messages
        //Task<Mezon.Protobuf.Realtime.ChannelMessageAck> SendChannelMessageAsync(Mezon.Protobuf.Realtime.ChannelMessageSend body, RequestOptions? options = null);
        //Task UpdateChannelMessageAsync(Mezon.Protobuf.Realtime.ChannelMessageUpdate body, RequestOptions? options = null);
        //Task DeleteChannelMessageAsync(Mezon.Protobuf.Realtime.ChannelMessageRemove body, RequestOptions? options = null);
        //#endregion

        //#region Mezon Meet Participant
        //Task RemoveParticipantMezonMeetAsync(Mezon.Net.Internal.Protos.MeetParticipantRequest body, RequestOptions? options = null);
        //Task MuteParticipantMezonMeetAsync(Mezon.Net.Internal.Protos.MeetParticipantRequest body, RequestOptions? options = null);
        //#endregion

        //#region Room Channel Apps
        //Task<Mezon.Net.Internal.Protos.CreateRoomChannelApps> CreateRoomChannelAppsAsync(Mezon.Net.Internal.Protos.CreateRoomChannelApps body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.GenerateHashChannelAppsResponse> GenerateHashChannelAppsAsync(Mezon.Net.Internal.Protos.GenerateHashChannelAppsRequest body, RequestOptions? options = null);
        //#endregion

        //#region OAuth Client
        //Task<Mezon.Net.Internal.Protos.MezonOauthClient> GetMezonOauthClientAsync(Mezon.Net.Internal.Protos.GetMezonOauthClientRequest body, RequestOptions? options = null);
        //Task DeleteMezonOauthClientAsync(Mezon.Net.Internal.Protos.MezonOauthClient body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.MezonOauthClient> UpdateMezonOauthClientAsync(Mezon.Net.Internal.Protos.MezonOauthClient body, RequestOptions? options = null);
        //#endregion

        //#region SD Topics
        //Task<Mezon.Net.Internal.Protos.SdTopicList> ListSdTopicAsync(Mezon.Net.Internal.Protos.ListSdTopicRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.SdTopic> GetTopicDetailAsync(Mezon.Net.Internal.Protos.SdTopicDetailRequest body, RequestOptions? options = null);
        //Task<Mezon.Net.Internal.Protos.SdTopic> CreateSdTopicAsync(Mezon.Net.Internal.Protos.SdTopicRequest body, RequestOptions? options = null);
        //Task DeleteSdTopicAsync(Mezon.Net.Internal.Protos.DeleteSdTopicRequest body, RequestOptions? options = null);
        //#endregion

        //#region Interactive
        //Task MessageButtonClickAsync(Mezon.Protobuf.Realtime.MessageButtonClicked body, RequestOptions? options = null);
        //Task DropdownBoxSelectedAsync(Mezon.Protobuf.Realtime.DropdownBoxSelected body, RequestOptions? options = null);
        //#endregion

        //#region Voice State
        //Task UpdateMezonVoiceStateAsync(Mezon.Protobuf.Realtime.HandleParticipantMeetStateEvent body, RequestOptions? options = null);
        //#endregion

        //#region Archived Thread
        //Task ActiveArchivedThreadAsync(Mezon.Protobuf.Realtime.ActiveArchivedThread body, RequestOptions? options = null);
        //#endregion

        //#region AI Agent
        //Task AddAgentToChannelAsync(Mezon.Net.Internal.Protos.UpdateAIAgentRequest body, RequestOptions? options = null);
        //Task DisconnectAgentAsync(Mezon.Net.Internal.Protos.UpdateAIAgentRequest body, RequestOptions? options = null);
        //#endregion

        //#region Report Message
        //Task ReportMessageAbuseAsync(Mezon.Net.Internal.Protos.ReportMessageAbuseReqest body, RequestOptions? options = null);
        //#endregion

        //#region Registration
        //Task<AuthenticationResponse> RegistrationEmailAsync(string basicAuthUsername, string basicAuthPassword, Mezon.Net.Internal.Protos.RegistrationEmailRequest body, RequestOptions? options = null);
        //#endregion

        //#region OAuth File Upload
        //Task<Mezon.Net.Internal.Protos.UploadAttachment> UploadOauthFileAsync(Mezon.Net.Internal.Protos.UploadAttachmentRequest body, RequestOptions? options = null);
        //#endregion

        //#region Account Update
        //Task UpdateAccountAsync(Mezon.Net.Internal.Protos.UpdateAccountRequest body, RequestOptions? options = null);
        //#endregion

        //#region Streaming Callback
        //Task<Mezon.Net.Internal.Protos.StreamHttpCallbackResponse> StreamingServerCallbackAsync(Mezon.Net.Internal.Protos.StreamHttpCallbackRequest body, RequestOptions? options = null);
        //#endregion

        //#region For Sale Items
        //Task<Mezon.Net.Internal.Protos.ForSaleItemList> ListForSaleItemsAsync(Mezon.Net.Internal.Protos.ListForSaleItemsRequest body, RequestOptions? options = null);
        //#endregion

        //#region Clan Webhook Handler
        //Task HandleClanWebhookAsync(Mezon.Net.Internal.Protos.ClanWebhookHandlerRequest body, RequestOptions? options = null);
        //#endregion
    }
}
