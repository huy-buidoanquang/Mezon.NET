using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Api;
using Mezon.NET.Api.ApiRequests;
using Mezon.NET.Api.ApiResponses;

namespace Mezon.NET.Abstractions
{
    public interface IMezonApiClient
    {
        protected string GatewayBasePath { get; }
        protected string ApiBasePath { get; }

        void ConfigureMezonApiBasePath(string apiBasePath);
        // Health check
        Task<object> HealthcheckAsync(string bearerToken);

        // Account management
        Task DeleteAccountAsync(string bearerToken);
        Task<AccountResponse> GetAccountAsync(string bearerToken);
        Task UpdateAccountAsync(string bearerToken, UpdateAccountRequest body);

        // Authentication
        Task<AuthenticationResponse> CheckLoginRequestAsync(string basicAuthUsername, string basicAuthPassword, ApiConfirmLoginRequest body);
        Task ConfirmLoginAsync(string bearerToken, string basePath, ConfirmLoginRequest body);
        Task<LoginIDResponse> CreateQRLoginAsync(string basicAuthUsername, string basicAuthPassword, LoginIDRequest body);
        Task<AuthenticationResponse> AuthenticateEmailAsync(string basicAuthUsername, string basicAuthPassword, EmailAuthenticationRequest body);
        Task<AuthenticationResponse> AuthenticateMezonAsync(string basicAuthUsername, string basicAuthPassword, AccountMezonRequest account, bool? create = null, string username = null, bool? isRemember = null);

        // Account linking
        Task LinkEmailAsync(string bearerToken, AccountEmailRequest body);
        Task LinkMezonAsync(string bearerToken, AccountMezonRequest body);
        Task UnlinkEmailAsync(string bearerToken, AccountEmailRequest body);
        Task UnlinkMezonAsync(string bearerToken, AccountMezonRequest body);

        // Registration and session
        Task<AuthenticationResponse> RegisterEmailAsync(string bearerToken, RegistrationEmailRequest body);
        Task<AuthenticationResponse> RefreshSessionAsync(string basicAuthUsername, string basicAuthPassword, SessionRefreshRequest body, CancellationToken cancellationToken = default);

        // Activity management
        Task<UserActivitiesResponse> GetActivitiesAsync(string bearerToken);
        Task<UserActivityResponse> CreateActiviyAsync(string bearerToken, CreateActivityRequest body);

        // Application management
        Task<AuthenticationResponse> AuthenticateAppAsync(string basicAuthUsername, string basicAuthPassword, AppAuthenticationRequest body, CancellationToken cancellationToken = default);
        Task<AppResponse> AddAppAsync(string bearerToken, AddAppRequest body);
        Task<AppsResponse> GetAppsAsync(string bearerToken, string filter = null, bool? tombstones = null, string cursor = null);
        Task AddAppToClanAsync(string bearerToken, string appId, string clanId);
        Task DeleteAppAsync(string bearerToken, string id, bool? recordDeletion = null);
        Task<AppResponse> GetAppAsync(string bearerToken, string id);
        Task<AppResponse> UpdateAppAsync(string bearerToken, string id, MezonUpdateAppRequest body);
        Task BanAppAsync(string bearerToken, string id);
        Task UnbanAppAsync(string bearerToken, string id);

        // Audit log
        Task<AuditLogsResponse> GetAuditLogsAsync(string bearerToken, string actionLog = null, string userId = null, string clanId = null, string dateLog = null);

        #region Category
        Task<CategoryDescriptionResponse> CreateCategoryDescriptionAsync(string bearerToken, CreateCategoryDescriptionRequest body);
        Task UpdateCategoryAsync(string bearerToken, string clanId, UpdateCategoryRequest body);
        Task UpdateCategoryOrderAsync(string bearerToken, UpdateCategoryOrdersRequest body);
        Task<CategoryDescriptionsResponse> GetCategoryDescriptionsAsync(string bearerToken, string clanId, string creatorId = null, string categoryName = null, string categoryId = null, int? categoryOrder = null);
        Task DeleteCategoryDescriptionAsync(string bearerToken, string categoryId, string clanId, string? categoryLabel = null);
        #endregion

        #region Friends
        Task AddFriendsAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null);
        Task BlockFriendsAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null);
        Task UnblockFriendsAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null);
        Task DeleteFriendsAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null);
        Task<FriendsResponse> GetFriendsAsync(string bearerToken, int? state = null, int? limit = null, string cursor = null);
        #endregion

        #region Clan
        Task<ClanDescriptionsResponse> GetClanDescriptionsAsync(string bearerToken, int? limit = null, int? state = null, string? cusor = null, CancellationToken cancellationToken = default);
        Task<ClanDescriptionResponse> CreateClanDescriptionAsync(string bearerToken, CreateClanDescriptionRequest body);
        Task DeleteClanDescriptionAsync(string bearerToken, string clanId);
        Task UpdateClanDescriptionAsync(string bearerToken, string clanId, object body);
        Task<ClanDescriptionProfileResponse> GetClanDescriptionProfileAsync(string bearerToken, string clanId);
        Task UpdateClanDescriptionProfileAsync(string bearerToken, string clanId, object body);
        Task<ClanUsersResponse> GetClanUsersAsync(string bearerToken, string clanId);
        Task KickClanUsersAsync(string bearerToken, string clanId, IEnumerable<string> userIds);
        Task<CheckDuplicateClanNameResponse> CheckDuplicateClanNameAsync(string bearerToken, string clanName);
        #endregion

        #region Channel
        Task<ChannelDescriptionResponse> CreateChannelDescriptionAsync(string bearerToken, CreateChannelDescriptionRequest body);
        Task DeleteChannelDescAsync(string bearerToken, string channelId);
        Task UpdateChannelDescriptionAsync(string bearerToken, string channelId, object body);
        Task AddChannelUsersAsync(string bearerToken, string channelId, IEnumerable<string> userIds);
        Task RemoveChannelUsersAsync(string bearerToken, string channelId, IEnumerable<string> userIds);
        Task<ChannelMessagesResponse> GetChannelMessagesAsync(string bearerToken, string clanId, string channelId, string messageId, int? direction = null, int? limit = null, string? topicId = null);
        Task<ChannelUsersResponse> GetChannelUsersAsync(string bearerToken, string clanId, string channelId, string channelType, int? limit = null, int? state = null, string cursor = null);
        //Task<ChannelAppsResponse> GetChannelAppsAsync(string bearerToken, string? clanId = null, CancellationToken cancellationToken = default);
        #endregion

        #region User
        Task<UsersResponse> GetUsersAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null);
        Task UpdateUserStatusAsync(string bearerToken, UpdateUserStatusRequest body);
        Task<UserStatusResponse> GetUserStatusAsync(string bearerToken);
        #endregion

        #region Roles
        Task<RoleResponse> CreateRoleAsync(string bearerToken, CreateRoleRequest body);
        Task DeleteRoleAsync(string bearerToken, string roleId, string? channelId = null, string? clanId = null, string? roleLabel = null);
        Task UpdateRoleAsync(string bearerToken, string roleId, UpdateRoleRequest body);
        Task<RoleEventResponse> GetRolesAsync(string bearerToken, string clanId, int? limit = null, int? state = null, string cursor = null);
        #endregion


        #region Storage
        Task<UploadAttachmentResponse> UploadAttachmentFileAsync(string bearerToken, UploadAttachmentRequest body);
        #endregion

        #region Events
        Task<EventManagementResponse> CreateEventAsync(string bearerToken, CreateEventRequest body);
        Task DeleteEventAsync(string bearerToken, string eventId, string clanId, string creatorId, string eventLabel = null, string channelId = null);
        Task UpdateEventUserAsync(string bearerToken, UpdateEventUserRequest body);
        Task UpdateEventAsync(string bearerToken, string eventId, UpdateEventRequest body);
        Task<EventManagementsResponse> GetEventsAsync(string bearerToken, string? clanId = null);
        Task AddUserEventAsync(string bearerToken, AddUserEventRequest body);
        Task DeleteUserEventAsync(string bearerToken, string clanId, string eventId);
        #endregion

        #region Permissions
        Task<PermissionsResponse> GetPermissionsAsync(string bearerToken);
        Task<PermissionsResponse> GetRolePermissionsAsync(string bearerToken, string roleId);
        Task<RoleUsersResponse> ListRoleUsersAsync(string bearerToken, string roleId, int? limit = null, string cursor = null);
        Task<UserPermissionsInChannelResponse> GetUserPermissionsInChannelAsync(string bearerToken, string clanId, string channelId);
        #endregion

        #region Invites
        Task<LinkInviteUserResponse> CreateLinkInviteUserAsync(string bearerToken, LinkInviteUserRequest body);
        Task<InviteUserResponse> GetLinkInviteAsync(string basicAuthUsername, string basicAuthPassword, string inviteId);
        Task<InviteUserResponse> InviteUserAsync(string bearerToken, string inviteId);
        #endregion

        #region Notification Settings
        Task SetNotificationClanSettingAsync(string bearerToken, SetDefaultNotificationRequest body);
        Task SetNotificationChannelSettingAsync(string bearerToken, SetNotificationChannelRequest body);
        Task SetMuteNotificationCategoryAsync(string bearerToken, SetMuteNotificationRequest body);
        Task SetMuteNotificationChannelAsync(string bearerToken, SetMuteNotificationRequest body);
        Task DeleteNotificationsAsync(string bearerToken, IEnumerable<string>? ids = null, string? category = null);
        Task<NotificationsResponse> GetNotificationsAsync(string bearerToken, string? clanId = null, string? notificationId = null, string? category = null, int? limit = null, int? direction = null);
        Task<NotificationChannelCategorySettingsResponse> GetChannelCategoryNotificationSettingsAsync(string bearerToken, string clanId);
        Task<ClanNotificationSettingResponse> GetClanNotificationSettingAsync(string bearerToken, string clanId);
        #endregion

        #region Messages (Advanced)
        //Task<SearchMessageResponse> SearchMessageAsync(string bearerToken, SearchMessageRequest body);
        //Task<ChannelMessageHeaderResponse> CreatePinMessageAsync(string bearerToken, PinMessageRequest body);
        //Task<PinMessagesListResponse> GetPinMessagesListAsync(string bearerToken, string channelId, string clanId);
        //Task DeletePinMessageAsync(string bearerToken, string messageId, string channelId, string clanId);
        //Task MarkAsReadAsync(string bearerToken, MarkAsReadRequest body);
        #endregion

        //#region Emoji & Stickers
        //Task CreateClanEmojiAsync(string bearerToken, ClanEmojiCreateRequest body);
        //Task UpdateClanEmojiByIdAsync(string bearerToken, string emojiId, UpdateClanEmojiRequest body);
        //Task DeleteClanEmojiByIdAsync(string bearerToken, string emojiId, string clanId);
        //Task AddClanStickerAsync(string bearerToken, ClanStickerAddRequest body);
        //Task UpdateClanStickerByIdAsync(string bearerToken, string stickerId, UpdateClanStickerRequest body);
        //Task DeleteClanStickerByIdAsync(string bearerToken, string stickerId, string clanId);
        //Task<EmojiListedResponse> GetListEmojisByUserIdAsync(string bearerToken);
        //Task<StickerListedResponse> GetListStickersByUserIdAsync(string bearerToken);
        //#endregion

        //#region Webhooks
        //Task<WebhookGenerateResponse> GenerateWebhookAsync(string bearerToken, WebhookCreateRequest body);
        //Task<WebhookListResponse> ListWebhookByChannelIdAsync(string bearerToken, string channelId, string clanId);
        //Task UpdateWebhookByIdAsync(string bearerToken, string webhookId, UpdateWebhookRequest body);
        //Task DeleteWebhookByIdAsync(string bearerToken, string webhookId, DeleteWebhookRequest body);
        //#endregion

        //#region System Messages
        //Task<SystemMessagesListResponse> GetSystemMessagesListAsync(string bearerToken);
        //Task<SystemMessageResponse> GetSystemMessageByClanIdAsync(string bearerToken, string clanId);
        //Task CreateSystemMessageAsync(string bearerToken, SystemMessageRequest body);
        //Task UpdateSystemMessageAsync(string bearerToken, string clanId, UpdateSystemMessageRequest body);
        //Task DeleteSystemMessageAsync(string bearerToken, string clanId);
        //#endregion

        //#region Ordering
        //Task UpdateRoleOrderAsync(string bearerToken, UpdateRoleOrderRequest body);
        //Task UpdateClanOrderAsync(string bearerToken, UpdateClanOrderRequest body);
        //#endregion

        //#region Encryption
        //Task<ChanEncryptionMethodResponse> GetChanEncryptionMethodAsync(string bearerToken, string channelId);
        //Task SetChanEncryptionMethodAsync(string bearerToken, string channelId, SetChanEncryptionMethodRequest body);
        //Task<GetPubKeysResponse> GetPublicKeysAsync(string bearerToken, IEnumerable<string> userIds);
        //Task PushPublicKeyAsync(string bearerToken, PushPublicKeyRequest body);
        //Task<GetKeyServerResponse> GetKeyServerAsync(string bearerToken);
        //#endregion

        //#region Onboarding
        //Task<ListOnboardingResponse> ListOnboardingAsync(string bearerToken, string clanId, int? guideType = null);
        //Task<OnboardingItemResponse> GetOnboardingDetailAsync(string bearerToken, string id, string clanId);
        //Task CreateOnboardingAsync(string bearerToken, CreateOnboardingRequest body);
        //Task UpdateOnboardingAsync(string bearerToken, string id, UpdateOnboardingRequest body);
        //Task DeleteOnboardingAsync(string bearerToken, string id, string clanId);
        //#endregion

        //#region Wallet & Transactions
        //Task GiveCoffeeAsync(string bearerToken, GiveCoffeeRequest body);
        //Task SendTokenAsync(string bearerToken, TokenSentRequest body);
        //Task<TransactionDetailResponse> ListTransactionDetailAsync(string bearerToken, string transId);
        //Task<WalletLedgerListResponse> ListWalletLedgerAsync(string bearerToken, int? limit = null, int? filter = null, int? page = null);
        //#endregion

        //#region Mezon Meet
        //Task<GenerateMeetTokenResponse> GenerateMeetTokenAsync(string bearerToken, GenerateMeetTokenRequest body);
        //Task<GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(string bearerToken);
        //Task<GenerateMeetTokenExternalResponse> GenerateMeetTokenExternalAsync(string basePath, string token, string displayName, bool? isGuest);
        //#endregion

        //#region Ownership
        //Task TransferOwnershipAsync(string bearerToken, TransferOwnershipRequest body);
        //#endregion
    }
}
