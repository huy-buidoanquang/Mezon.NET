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
        event Func<string, string, double, Task> SentRequest;

        LoginState LoginState { get; }

        internal MezonRequestQueue RequestQueue { get; }

        Task LoginAsync(TokenType tokenType, string token, RequestOptions? options = null);

        Task LogoutAsync();

        long? CurrentUserId { get; }

        TokenType TokenType { get; }

        string AuthToken { get; }

        Task SendNoResAsync(string method, string endpoint, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task SendJsonNoResAsync(string method, string endpoint, object payload, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task SendMultipartNoResAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task<Stream> SendAsync(string method, string endpoint, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task<Stream> SendJsonAsync(string method, string endpoint, object payload, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        Task<Stream> SendMultipartAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null);

        void ConfigureGatewayBasePath(string gatewayBasePath);

        void ConfigureApiBasePath(string apiBasePath);

        // Health check
        //Task<object> HealthcheckAsync();

        // Account management
        Task DeleteAccountAsync();
        Task<Account> GetAccountAsync();
        //Task UpdateAccountAsync(UpdateAccountRequest body, RequestOptions? options = null);

        //// Authentication
        Task<AuthenticationResponse> CheckLoginRequestAsync(string basicAuthUsername, string basicAuthPassword, Api.ConfirmLoginRequest body, RequestOptions? options = null);
        Task ConfirmLoginAsync(Api.ConfirmLoginRequest body, RequestOptions options);
        Task<Api.LoginIDResponse> CreateQRLoginAsync(string basicAuthUsername, string basicAuthPassword, LoginIDRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> AuthenticateEmailAsync(string basicAuthUsername, string basicAuthPassword, EmailAuthenticationRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> AuthenticateMezonAsync(string basicAuthUsername, string basicAuthPassword, AccountMezonRequest body, AccountMezonParams args, RequestOptions? options = null);
        Task<AccountConfirmResponse> AuthenticateSMSOTPAsync(string basicAuthUsername, string basicAuthPassword, AuthenticateSMSRequest body, RequestOptions? options = null);

        //// Account linking
        //Task LinkEmailAsync(AccountEmailRequest body, RequestOptions? options = null);
        //Task LinkMezonAsync(AccountMezonRequest body, RequestOptions? options = null);
        //Task UnlinkEmailAsync(AccountEmailRequest body, RequestOptions? options = null);
        //Task UnlinkMezonAsync(AccountMezonRequest body, RequestOptions? options = null);

        //// Registration and session
        //Task<AuthenticationResponse> RegisterEmailAsync(RegistrationEmailRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> RefreshSessionAsync(string basicAuthUsername, string basicAuthPassword, Api.SessionRefreshRequest body, RequestOptions? options = null);

        //// Activity management
        //Task<UserActivitiesResponse> GetActivitiesAsync(string bearerToken);
        //Task<UserActivityResponse> CreateActiviyAsync(CreateActivityRequest body, RequestOptions? options = null);

        // Application management
        Task<AuthenticationResponse> AuthenticateAppAsync(string basicAuthUsername, string basicAuthPassword, AppAuthenticationRequest body, RequestOptions? options = null);
        Task<bool> AuthenticateAppLogoutAsync(AppAuthenticationLogoutRequest body, RequestOptions? options = null);
        //Task<AppResponse> AddAppAsync(AddAppRequest body, RequestOptions? options = null);
        //Task<AppsResponse> GetAppsAsync(string filter = null, bool? tombstones = null, string cursor = null);
        //Task AddAppToClanAsync(string appId, string clanId);
        //Task DeleteAppAsync(string id, bool? recordDeletion = null);
        //Task<AppResponse> GetAppAsync(string id);
        //Task<AppResponse> UpdateAppAsync(string id, MezonUpdateAppRequest body, RequestOptions? options = null);
        //Task BanAppAsync(string id);
        //Task UnbanAppAsync(string id);

        //// Audit log
        //Task<AuditLogsResponse> GetAuditLogsAsync(string actionLog = null, string userId = null, string clanId = null, string dateLog = null);

        //#region Category
        //Task<CategoryDescriptionResponse> CreateCategoryDescriptionAsync(CreateCategoryDescriptionRequest body, RequestOptions? options = null);
        //Task UpdateCategoryAsync(string clanId, UpdateCategoryRequest body, RequestOptions? options = null);
        //Task UpdateCategoryOrderAsync(UpdateCategoryOrdersRequest body, RequestOptions? options = null);
        //Task<CategoryDescriptionsResponse> GetCategoryDescriptionsAsync(string clanId, string creatorId = null, string categoryName = null, string categoryId = null, int? categoryOrder = null);
        //Task DeleteCategoryDescriptionAsync(string categoryId, string clanId, string? categoryLabel = null);
        //#endregion

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

        //#region Storage
        //Task<UploadAttachmentResponse> UploadAttachmentFileAsync(UploadAttachmentRequest body, RequestOptions? options = null);
        //#endregion

        #region Events
        Task<Mezon.Protobuf.Api.EventManagement> CreateEventAsync(Mezon.Protobuf.Api.CreateEventRequest body, RequestOptions? options = null);
        Task DeleteEventAsync(long eventId, RequestOptions? options = null);
        Task UpdateEventAsync(Mezon.Protobuf.Api.UpdateEventRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.EventList> ListEventsAsync(long? clanId = null, RequestOptions? options = null);
        //Task UpdateEventUserAsync(UpdateEventUserRequest body, RequestOptions? options = null);
        //Task AddUserEventAsync(AddUserEventRequest body, RequestOptions? options = null);
        //Task DeleteUserEventAsync(string clanId, string eventId);
        #endregion

        //#region Permissions
        //Task<PermissionsResponse> GetPermissionsAsync(string bearerToken);
        //Task<PermissionsResponse> GetRolePermissionsAsync(string roleId);
        //Task<RoleUsersResponse> ListRoleUsersAsync(string roleId, int? limit = null, string cursor = null);
        //Task<UserPermissionsInChannelResponse> GetUserPermissionsInChannelAsync(string clanId, string channelId);
        //#endregion

        //#region Invites
        //Task<LinkInviteUserResponse> CreateLinkInviteUserAsync(LinkInviteUserRequest body, RequestOptions? options = null);
        //Task<InviteUserResponse> GetLinkInviteAsync(string basicAuthUsername, string basicAuthPassword, string inviteId);
        //Task<InviteUserResponse> InviteUserAsync(string inviteId);
        //#endregion

        //#region Notification Settings
        //Task SetNotificationClanSettingAsync(SetDefaultNotificationRequest body, RequestOptions? options = null);
        //Task SetNotificationChannelSettingAsync(SetNotificationChannelRequest body, RequestOptions? options = null);
        //Task SetMuteNotificationCategoryAsync(SetMuteNotificationRequest body, RequestOptions? options = null);
        //Task SetMuteNotificationChannelAsync(SetMuteNotificationRequest body, RequestOptions? options = null);
        //Task DeleteNotificationsAsync(IEnumerable<string>? ids = null, string? category = null);
        //Task<NotificationsResponse> GetNotificationsAsync(string? clanId = null, string? notificationId = null, string? category = null, int? limit = null, int? direction = null);
        //Task<NotificationChannelCategorySettingsResponse> GetChannelCategoryNotificationSettingsAsync(string clanId);
        //Task<ClanNotificationSettingResponse> GetClanNotificationSettingAsync(string clanId);
        //#endregion

        #region Messages (Advanced)
        Task<Mezon.Protobuf.Api.SearchMessageResponse> SearchMessageAsync(Mezon.Protobuf.Api.SearchMessageRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.ChannelMessage> CreatePinMessageAsync(Mezon.Protobuf.Api.PinMessageRequest body, RequestOptions? options = null);
        Task<Mezon.Protobuf.Api.PinMessagesList> GetPinMessagesListAsync(long channelId, long clanId, RequestOptions? options = null);
        Task DeletePinMessageAsync(long messageId, long channelId, long clanId, RequestOptions? options = null);
        Task MarkAsReadAsync(Mezon.Protobuf.Api.MarkAsReadRequest body, RequestOptions? options = null);
        #endregion

        //#region Emoji & Stickers
        //Task CreateClanEmojiAsync(ClanEmojiCreateRequest body, RequestOptions? options = null);
        //Task UpdateClanEmojiByIdAsync(string emojiId, UpdateClanEmojiRequest body, RequestOptions? options = null);
        //Task DeleteClanEmojiByIdAsync(string emojiId, string clanId);
        //Task AddClanStickerAsync(ClanStickerAddRequest body, RequestOptions? options = null);
        //Task UpdateClanStickerByIdAsync(string stickerId, UpdateClanStickerRequest body, RequestOptions? options = null);
        //Task DeleteClanStickerByIdAsync(string stickerId, string clanId);
        //Task<EmojiListedResponse> GetListEmojisByUserIdAsync(string bearerToken);
        //Task<StickerListedResponse> GetListStickersByUserIdAsync(string bearerToken);
        //#endregion

        //#region Webhooks
        //Task<WebhookGenerateResponse> GenerateWebhookAsync(WebhookCreateRequest body, RequestOptions? options = null);
        //Task<WebhookListResponse> ListWebhookByChannelIdAsync(string channelId, string clanId);
        //Task UpdateWebhookByIdAsync(string webhookId, UpdateWebhookRequest body, RequestOptions? options = null);
        //Task DeleteWebhookByIdAsync(string webhookId, DeleteWebhookRequest body, RequestOptions? options = null);
        //#endregion

        //#region System Messages
        //Task<SystemMessagesListResponse> GetSystemMessagesListAsync(string bearerToken);
        //Task<SystemMessageResponse> GetSystemMessageByClanIdAsync(string clanId);
        //Task CreateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null);
        //Task UpdateSystemMessageAsync(string clanId, UpdateSystemMessageRequest body, RequestOptions? options = null);
        //Task DeleteSystemMessageAsync(string clanId);
        //#endregion

        //#region Ordering
        //Task UpdateRoleOrderAsync(UpdateRoleOrderRequest body, RequestOptions? options = null);
        //Task UpdateClanOrderAsync(UpdateClanOrderRequest body, RequestOptions? options = null);
        //#endregion

        //#region Encryption
        //Task<ChanEncryptionMethodResponse> GetChanEncryptionMethodAsync(string channelId);
        //Task SetChanEncryptionMethodAsync(string channelId, SetChanEncryptionMethodRequest body, RequestOptions? options = null);
        //Task<GetPubKeysResponse> GetPublicKeysAsync(IEnumerable<string> userIds);
        //Task PushPublicKeyAsync(PushPublicKeyRequest body, RequestOptions? options = null);
        //Task<GetKeyServerResponse> GetKeyServerAsync(string bearerToken);
        //#endregion

        //#region Onboarding
        //Task<ListOnboardingResponse> ListOnboardingAsync(string clanId, int? guideType = null);
        //Task<OnboardingItemResponse> GetOnboardingDetailAsync(string id, string clanId);
        //Task CreateOnboardingAsync(CreateOnboardingRequest body, RequestOptions? options = null);
        //Task UpdateOnboardingAsync(string id, UpdateOnboardingRequest body, RequestOptions? options = null);
        //Task DeleteOnboardingAsync(string id, string clanId);
        //#endregion

        //#region Wallet & Transactions
        //Task GiveCoffeeAsync(GiveCoffeeRequest body, RequestOptions? options = null);
        //Task SendTokenAsync(TokenSentRequest body, RequestOptions? options = null);
        //Task<TransactionDetailResponse> ListTransactionDetailAsync(string transId);
        //Task<WalletLedgerListResponse> ListWalletLedgerAsync(int? limit = null, int? filter = null, int? page = null);
        //#endregion

        //#region Mezon Meet
        //Task<GenerateMeetTokenResponse> GenerateMeetTokenAsync(GenerateMeetTokenRequest body, RequestOptions? options = null);
        //Task<GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(string bearerToken);
        //Task<GenerateMeetTokenExternalResponse> GenerateMeetTokenExternalAsync(string basePath, string token, string displayName, bool? isGuest);
        //#endregion

        //#region Ownership
        //Task TransferOwnershipAsync(TransferOwnershipRequest body, RequestOptions? options = null);
        //#endregion
    }
}
