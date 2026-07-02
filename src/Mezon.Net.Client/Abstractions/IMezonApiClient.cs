using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mezon.Net.Api;
using Mezon.Net.Core;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
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

        Task<Api.LoginIDResponse> CreateQRLoginAsync(string basicAuthUsername, string basicAuthPassword, LoginIDRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> AuthenticateEmailAsync(string basicAuthUsername, string basicAuthPassword, EmailAuthenticationRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> RefreshSessionAsync(string basicAuthUsername, string basicAuthPassword, Api.SessionRefreshRequest body, RequestOptions? options = null);
        Task<AuthenticationResponse> AuthenticateAppAsync(string basicAuthUsername, string basicAuthPassword, AppAuthenticationRequest body, RequestOptions? options = null);
        Task<bool> AuthenticateAppLogoutAsync(AppAuthenticationLogoutRequest body, RequestOptions? options = null);
        Task<ClanDescList> ListClanDescsAsync(PaginationParams args, RequestOptions? options = null);
        Task DeleteAccountAsync(RequestOptions? options = null);
        Task<Account> GetAccountAsync(RequestOptions? options = null);
        Task<AddFriendsResponse> AddFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        Task BlockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        Task UnblockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        Task DeleteFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null);
        Task<FriendList> ListFriendsAsync(int? state = null, int? limit = null, string? cursor = null, RequestOptions? options = null);
        Task<ClanDesc> CreateClanDescAsync(string clanName, string? logo = null, string? banner = null, RequestOptions? options = null);
        Task DeleteClanDescAsync(long clanId, RequestOptions? options = null);
        Task UpdateClanDescAsync(UpdateClanDescRequest body, RequestOptions? options = null);
        Task<ClanUserList> ListClanUsersAsync(long clanId, RequestOptions? options = null);
        Task RemoveClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null);
        Task BanClanUsersAsync(long clanId, long channelId, IEnumerable<long> userIds, int? banTime = null, string? reason = null, RequestOptions? options = null);
        Task<Internal.Api.ChannelDescription> CreateChannelDescAsync(CreateChannelDescRequest body, RequestOptions? options = null);
        Task DeleteChannelDescAsync(long channelId, RequestOptions? options = null);
        Task UpdateChannelDescAsync(UpdateChannelDescRequest body, RequestOptions? options = null);
        Task AddChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null);
        Task RemoveChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null);
        Task<ChannelMessageList> ListChannelMessagesAsync(long clanId, long channelId, long? messageId = null, int? direction = null, int? limit = null, long? topicId = null, RequestOptions? options = null);
        Task<ChannelUserList> ListChannelUsersAsync(long clanId, long channelId, int channelType, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null);
        Task DeleteRoleAsync(long roleId, RequestOptions? options = null);
        Task<RoleListEventResponse> ListRolesAsync(long? clanId = null, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null);
        Task UpdateUserAsync(UpdateUsersRequest body, RequestOptions? options = null);
        Task DeleteEventAsync(long eventId, RequestOptions? options = null);
        Task<EventList> ListEventsAsync(long? clanId = null, RequestOptions? options = null);
        Task<ChannelMessage> CreatePinMessageAsync(PinMessageRequest body, RequestOptions? options = null);
        Task<PinMessagesList> GetPinMessagesListAsync(long channelId, long clanId, RequestOptions? options = null);
        Task DeletePinMessageAsync(long messageId, long channelId, long clanId, RequestOptions? options = null);
        Task MarkAsReadAsync(MarkAsReadRequest body, RequestOptions? options = null);
        Task CreateClanEmojiAsync(ClanEmojiCreateRequest body, RequestOptions? options = null);
        Task UpdateClanEmojiByIdAsync(ClanEmojiUpdateRequest body, RequestOptions? options = null);
        Task DeleteClanEmojiByIdAsync(long emojiId, long clanId, RequestOptions? options = null);
        Task AddClanStickerAsync(ClanStickerAddRequest body, RequestOptions? options = null);
        Task UpdateClanStickerByIdAsync(ClanStickerUpdateByIdRequest body, RequestOptions? options = null);
        Task DeleteClanStickerByIdAsync(long stickerId, long clanId, RequestOptions? options = null);
        Task<EmojiListedResponse> GetListEmojisByUserIdAsync(RequestOptions? options = null);
        Task<StickerListedResponse> GetListStickersByUserIdAsync(RequestOptions? options = null);
        Task<WebhookGenerateResponse> GenerateWebhookAsync(WebhookCreateRequest body, RequestOptions? options = null);
        Task<WebhookListResponse> ListWebhookByChannelIdAsync(long channelId, long clanId, RequestOptions? options = null);
        Task UpdateWebhookByIdAsync(WebhookUpdateRequestById body, RequestOptions? options = null);
        Task DeleteWebhookByIdAsync(WebhookDeleteRequestById body, RequestOptions? options = null);
        Task CreateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null);
        Task UpdateSystemMessageAsync(SystemMessageRequest body, RequestOptions? options = null);
        Task<SystemMessage> GetSystemMessageByClanIdAsync(long clanId, RequestOptions? options = null);
        Task DeleteSystemMessageAsync(long clanId, RequestOptions? options = null);
        Task UpdateRoleOrderAsync(UpdateRoleOrderRequest body, RequestOptions? options = null);
        Task UpdateClanOrderAsync(UpdateClanOrderRequest body, RequestOptions? options = null);
        Task<ChanEncryptionMethod> GetChanEncryptionMethodAsync(long channelId, RequestOptions? options = null);
        Task SetChanEncryptionMethodAsync(ChanEncryptionMethod body, RequestOptions? options = null);
        Task<GetPubKeysResponse> GetPublicKeysAsync(IEnumerable<long> userIds, RequestOptions? options = null);
        Task PushPublicKeyAsync(PushPubKeyRequest body, RequestOptions? options = null);
        Task<GetKeyServerResp> GetKeyServerAsync(RequestOptions? options = null);
        Task<ListOnboardingResponse> ListOnboardingAsync(long clanId, int? guideType = null, RequestOptions? options = null);
        Task<OnboardingItem> GetOnboardingDetailAsync(long id, long clanId, RequestOptions? options = null);
        Task<ListOnboardingResponse> CreateOnboardingAsync(CreateOnboardingRequest body, RequestOptions? options = null);
        Task UpdateOnboardingAsync(UpdateOnboardingRequest body, RequestOptions? options = null);
        Task DeleteOnboardingAsync(long id, long clanId, RequestOptions? options = null);
        Task<ListUserActivity> ListActivityAsync(RequestOptions? options = null);
        Task<GenerateMeetTokenResponse> GenerateMeetTokenAsync(GenerateMeetTokenRequest body, RequestOptions? options = null);
        Task TransferOwnershipAsync(TransferOwnershipRequest body, RequestOptions? options = null);
        Task<PermissionList> GetListPermissionAsync(RequestOptions? options = null);
        Task<PermissionList> ListRolePermissionsAsync(long roleId, RequestOptions? options = null);
        Task<RoleUserList> ListRoleUsersAsync(long roleId, int? limit = null, string? cursor = null, RequestOptions? options = null);
        Task<UserPermissionInChannelListResponse> ListUserPermissionInChannelAsync(long clanId, long channelId, RequestOptions? options = null);
        Task DeleteNotificationsAsync(IEnumerable<long>? ids = null, int? category = null, RequestOptions? options = null);
        Task<NotificationList> ListNotificationsAsync(long? clanId = null, long? notificationId = null, int? limit = null, int? category = null, int? direction = null, RequestOptions? options = null);
        Task<CategoryDesc> CreateCategoryDescAsync(CreateCategoryDescRequest body, RequestOptions? options = null);
        Task DeleteCategoryDescAsync(long categoryId, long clanId, RequestOptions? options = null);
        Task UpdateCategoryAsync(UpdateCategoryDescRequest body, RequestOptions? options = null);
        Task<CategoryDescList> ListCategoryDescsAsync(long clanId, RequestOptions? options = null);
        Task<InviteUserRes> InviteUserAsync(long inviteId, RequestOptions? options = null);
        Task SetNotificationChannelSettingAsync(SetNotificationRequest body, RequestOptions? options = null);
        Task SetMuteNotificationCategoryAsync(SetMuteRequest body, RequestOptions? options = null);
        Task SetMuteNotificationChannelAsync(SetMuteRequest body, RequestOptions? options = null);
        Task<NotificationChannelCategorySettingList> GetChannelCategoryNotificationSettingsAsync(long clanId, RequestOptions? options = null);
        Task<NotificationSetting> GetClanNotificationSettingAsync(long clanId, RequestOptions? options = null);
        Task<UserStatus> GetUserStatusAsync(RequestOptions? options = null);
        Task UpdateUserStatusAsync(UserStatusUpdate body, RequestOptions? options = null);
        Task<AppList> ListAppsAsync(string? filter = null, bool? tombstones = null, string? cursor = null, RequestOptions? options = null);
        Task<App> GetAppAsync(long id, RequestOptions? options = null);
        Task<App> UpdateAppAsync(UpdateAppRequest body, RequestOptions? options = null);
        Task DeleteAppAsync(long id, bool? recordDeletion = null, RequestOptions? options = null);
        Task AddAppToClanAsync(long appId, long clanId, RequestOptions? options = null);
        Task<ListAuditLog> ListAuditLogAsync(long? clanId = null, string? actionLog = null, long? userId = null, string? dateLog = null, RequestOptions? options = null);
        Task AddUserEventAsync(UserEventRequest body, RequestOptions? options = null);
        Task DeleteUserEventAsync(long clanId, long eventId, RequestOptions? options = null);
        Task HealthcheckAsync(RequestOptions? options = null);
        Task<ChannelDescList> ListChannelDescsAsync(long clanId, int? limit = null, int? state = null, string? cursor = null, int? channelType = null, bool? isMobile = null, int? page = null, RequestOptions? options = null);
        Task<Internal.Api.ChannelDescription> GetChannelDetailAsync(long channelId, RequestOptions? options = null);
        Task<BannedUserList> ListBannedUsersAsync(long clanId, RequestOptions? options = null);
        Task UnbanClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null);
        Task<RegistFcmDeviceTokenResponse> RegistFCMDeviceTokenAsync(RegistFcmDeviceTokenRequest body, RequestOptions? options = null);
        Task<AllUserClans> ListUserClansByUserIdAsync(RequestOptions? options = null);
        Task<ListChannelAppsResponse> ListChannelAppsAsync(long? clanId = null, RequestOptions? options = null);
        Task CloseDMByChannelIdAsync(long channelId, RequestOptions? options = null);
        Task OpenDMByChannelIdAsync(long channelId, RequestOptions? options = null);
        Task<ClanProfile> GetUserProfileOnClanAsync(long clanId, RequestOptions? options = null);
        Task UpdateUserProfileByClanAsync(UpdateClanProfileRequest body, RequestOptions? options = null);
        Task LeaveThreadAsync(long channelId, RequestOptions? options = null);
        Task<ChannelDescListNoPool> ListThreadDescsAsync(long channelId, RequestOptions? options = null);
        Task<ChannelDescList> SearchThreadAsync(SearchThreadRequest body, RequestOptions? options = null);
        Task<LinkAccountConfirmRequest> LinkSMSAsync(AccountMezon body, RequestOptions? options = null);
        Task ConfirmLinkMezonOTPAsync(LinkAccountConfirmRequest body, RequestOptions? options = null);
        Task<LinkAccountConfirmRequest> LinkEmailAsync(AccountEmail body, RequestOptions? options = null);
        Task UnlinkMezonAsync(AccountMezon body, RequestOptions? options = null);
        Task UnlinkEmailAsync(AccountEmail body, RequestOptions? options = null);
        Task<IsBannedResponse> IsBannedAsync(long channelId, RequestOptions? options = null);
        Task AddRolesChannelDescAsync(AddRoleChannelDescRequest body, RequestOptions? options = null);
        Task DeleteRoleChannelDescAsync(long roleId, RequestOptions? options = null);
        Task SetRoleChannelPermissionAsync(UpdateRoleChannelRequest body, RequestOptions? options = null);
        Task<RoleList> GetRoleOfUserInTheClanAsync(long clanId, RequestOptions? options = null);
        Task<PermissionRoleChannelListEventResponse> GetPermissionByRoleIdChannelIdAsync(PermissionRoleChannelListEventRequest body, RequestOptions? options = null);
        Task<ChannelAttachmentList> ListChannelAttachmentAsync(long channelId, RequestOptions? options = null);
        Task<VoiceChannelUserList> ListChannelVoiceUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null);
        Task<StreamingChannelUserList> ListStreamingChannelUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null);
        Task<ChannelDescListNoPool> ListChannelByUserIdAsync(RequestOptions? options = null);
        Task<NotificationUserChannel> GetNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null);
        Task<NotificationUserChannel> GetNotificationCategoryAsync(DefaultNotificationCategory body, RequestOptions? options = null);
        Task SetNotificationCategorySettingAsync(SetNotificationRequest body, RequestOptions? options = null);
        Task DeleteNotificationCategorySettingAsync(DefaultNotificationCategory body, RequestOptions? options = null);
        Task DeleteNotificationChannelAsync(NotificationChannel body, RequestOptions? options = null);
        Task<ChannelMessage> CreateMessage2InboxAsync(Message2InboxRequest body, RequestOptions? options = null);
        Task<ChannelSettingListResponse> ListChannelSettingAsync(long clanId, RequestOptions? options = null);
        Task UpdateChannelPrivateAsync(ChangeChannelPrivateRequest body, RequestOptions? options = null);
        Task ChangeChannelCategoryAsync(ChangeChannelCategoryRequest body, RequestOptions? options = null);
        Task<EmojiRecentList> EmojiRecentListAsync(RequestOptions? options = null);
        Task<AllUsersAddChannelResponse> ListChannelUsersUCAsync(AllUsersAddChannelRequest body, RequestOptions? options = null);
        Task<EditChannelCanvasResponse> EditChannelCanvasesAsync(EditChannelCanvasRequest body, RequestOptions? options = null);
        Task<ChannelCanvasListResponse> GetChannelCanvasListAsync(long channelId, RequestOptions? options = null);
        Task<ChannelCanvasDetailResponse> GetChannelCanvasDetailAsync(long id, RequestOptions? options = null);
        Task DeleteChannelCanvasAsync(long canvasId, RequestOptions? options = null);
        Task<ListFavoriteChannelResponse> GetListFavoriteChannelAsync(long clanId, RequestOptions? options = null);
        Task<AddFavoriteChannelResponse> AddChannelFavoriteAsync(AddFavoriteChannelRequest body, RequestOptions? options = null);
        Task RemoveChannelFavoriteAsync(long channelId, RequestOptions? options = null);
        Task<GenerateClanWebhookResponse> GenerateClanWebhookAsync(GenerateClanWebhookRequest body, RequestOptions? options = null);
        Task<ListClanWebhookResponse> ListClanWebhookAsync(long clanId, RequestOptions? options = null);
        Task UpdateClanWebhookByIdAsync(UpdateClanWebhookRequest body, RequestOptions? options = null);
        Task DeleteClanWebhookByIdAsync(long id, RequestOptions? options = null);
        Task<ListOnboardingStepResponse> ListOnboardingStepAsync(long clanId, RequestOptions? options = null);
        Task UpdateOnboardingStepAsync(UpdateOnboardingStepRequest body, RequestOptions? options = null);
        Task DeleteQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null);
        Task AddQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null);
        Task UpdateQuickMenuAccessAsync(QuickMenuAccess body, RequestOptions? options = null);
        Task<QuickMenuAccessList> ListQuickMenuAccessAsync(long botId, long channelId, int? menuType = null, RequestOptions? options = null);
        Task<IsFollowerResponse> IsFollowerAsync(IsFollowerRequest body, RequestOptions? options = null);
        Task<ChannelMessageAck> SendChannelMessageAsync(ChannelMessageSend body, RequestOptions? options = null);
        Task<ChannelMessageAck> SendChannelMessageAsync(in Mezon.Net.Api.SendChannelMessageParams message, RequestOptions? options = null);
        Task UpdateChannelMessageAsync(ChannelMessageUpdate body, RequestOptions? options = null);
        Task DeleteChannelMessageAsync(ChannelMessageRemove body, RequestOptions? options = null);
        Task RemoveParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null);
        Task MuteParticipantMezonMeetAsync(MeetParticipantRequest body, RequestOptions? options = null);
        Task<CreateRoomChannelApps> CreateRoomChannelAppsAsync(CreateRoomChannelApps body, RequestOptions? options = null);
        Task<GenerateHashChannelAppsResponse> GenerateHashChannelAppsAsync(GenerateHashChannelAppsRequest body, RequestOptions? options = null);
        Task<MezonOauthClient> GetMezonOauthClientAsync(GetMezonOauthClientRequest body, RequestOptions? options = null);
        Task DeleteMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null);
        Task<MezonOauthClient> UpdateMezonOauthClientAsync(MezonOauthClient body, RequestOptions? options = null);
        Task<SdTopicList> ListSdTopicAsync(ListSdTopicRequest body, RequestOptions? options = null);
        Task<SdTopic> GetTopicDetailAsync(SdTopicDetailRequest body, RequestOptions? options = null);
        Task<SdTopic> CreateSdTopicAsync(SdTopicRequest body, RequestOptions? options = null);
        Task DeleteSdTopicAsync(DeleteSdTopicRequest body, RequestOptions? options = null);
        Task MessageButtonClickAsync(MessageButtonClicked body, RequestOptions? options = null);
        Task DropdownBoxSelectedAsync(DropdownBoxSelected body, RequestOptions? options = null);
        Task ActiveArchivedThreadAsync(ActiveArchivedThread body, RequestOptions? options = null);
        Task AddAgentToChannelAsync(UpdateAIAgentRequest body, RequestOptions? options = null);
        Task DisconnectAgentAsync(UpdateAIAgentRequest body, RequestOptions? options = null);
        Task ReportMessageAbuseAsync(ReportMessageAbuseReqest body, RequestOptions? options = null);
        Task<StreamHttpCallbackResponse> StreamingServerCallbackAsync(StreamHttpCallbackRequest body, RequestOptions? options = null);
        Task<ForSaleItemList> ListForSaleItemsAsync(ListForSaleItemsRequest body, RequestOptions? options = null);
        Task HandleClanWebhookAsync(ClanWebhookHandlerRequest body, RequestOptions? options = null);
        Task<MutedChannelList> ListMutedChannelAsync(long clanId, RequestOptions? options = null);
        Task<ListClanBadgeCountResponse> ListClanBadgeCountAsync(RequestOptions? options = null);
        Task<ListChannelBadgeCountResponse> ListChannelBadgeCountAsync(long clanId, int? limit = null, int? page = null, RequestOptions? options = null);
        Task<LogedDeviceList> ListLogedDeviceAsync(RequestOptions? options = null);
        Task<ClanUserStatusList> ListClanUsersStatusAsync(long clanId, RequestOptions? options = null);
        Task<ListChannelTimelineResponse> ListChannelTimelineAsync(ListChannelTimelineRequest body, RequestOptions? options = null);
        Task<ListArchivedChannelDescsResponse> ListArchivedChannelDescsAsync(long clanId, RequestOptions? options = null);
        Task<ListUserOnlineResponse> ListUserOnlineAsync(long clanId, int? limit = null, int? page = null, RequestOptions? options = null);
        Task<global::Mezon.Net.Internal.Api.Session> RegistrationEmailAsync(global::Mezon.Net.Internal.Api.RegistrationEmailRequest body, RequestOptions? options = null);
        Task<UploadAttachment> UploadAttachmentFileAsync(global::Mezon.Net.Internal.Api.UploadAttachmentRequest body, RequestOptions? options = null);
        Task<UploadAttachment> UploadOauthFileAsync(global::Mezon.Net.Internal.Api.UploadAttachmentRequest body, RequestOptions? options = null);
        Task<Role> CreateRoleAsync(global::Mezon.Net.Internal.Api.CreateRoleRequest body, RequestOptions? options = null);
        Task<EventManagement> CreateEventAsync(global::Mezon.Net.Internal.Api.CreateEventRequest body, RequestOptions? options = null);
        Task ArchiveChannelAsync(ArchiveChannelRequest body, RequestOptions? options = null);
        Task<LinkInviteUser> CreateLinkInviteUserAsync(global::Mezon.Net.Internal.Api.LinkInviteUserRequest body, RequestOptions? options = null);
        Task SetNotificationClanSettingAsync(global::Mezon.Net.Internal.Api.SetDefaultNotificationRequest body, RequestOptions? options = null);
        Task UpdateAccountAsync(Internal.Api.UpdateAccountRequest body, RequestOptions? options = null);
        Task<global::Mezon.Net.Internal.Api.Session> UpdateUsernameAsync(UpdateUsernameRequest body, RequestOptions? options = null);
        Task UpdateCategoryOrderAsync(global::Mezon.Net.Internal.Api.UpdateCategoryOrderRequest body, RequestOptions? options = null);
        Task UpdateRoleAsync(global::Mezon.Net.Internal.Api.UpdateRoleRequest body, RequestOptions? options = null);
        Task UpdateEventAsync(global::Mezon.Net.Internal.Api.UpdateEventRequest body, RequestOptions? options = null);
        Task<global::Mezon.Net.Internal.Api.SearchMessageResponse> SearchMessageAsync(global::Mezon.Net.Internal.Api.SearchMessageRequest body, RequestOptions? options = null);
        Task HandleWebhookAsync(ClanWebhookHandlerRequest body, RequestOptions? options = null);
        Task<CheckDuplicateNameResponse> CheckDuplicateNameAsync(CheckDuplicateNameRequest body, RequestOptions? options = null);
        Task<App> AddAppAsync(global::Mezon.Net.Internal.Api.AddAppRequest body, RequestOptions? options = null);
        Task<UserActivity> CreateActivityAsync(global::Mezon.Net.Internal.Api.CreateActivityRequest body, RequestOptions? options = null);
        Task UpdateUserCustomStatusAsync(User body, RequestOptions? options = null);
        Task<global::Mezon.Net.Internal.Api.GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(RequestOptions? options = null);
        Task<UpdateChannelTimelineResponse> UpdateChannelTimelineAsync(UpdateChannelTimelineRequest body, RequestOptions? options = null);
        Task<CreateChannelTimelineResponse> CreateChannelTimelineAsync(CreateChannelTimelineRequest body, RequestOptions? options = null);
        Task<ChannelTimelineDetailResponse> DetailChannelTimelineAsync(ChannelTimelineDetailRequest body, RequestOptions? options = null);
        Task<CreatePollResponse> CreatePollAsync(CreatePollRequest body, RequestOptions? options = null);
        Task<VotePollResponse> VotePollAsync(VotePollRequest body, RequestOptions? options = null);
        Task ClosePollAsync(ClosePollRequest body, RequestOptions? options = null);
        Task<GetPollResponse> GetPollAsync(GetPollRequest body, RequestOptions? options = null);
        Task ReactChannelMessageAsync(MessageReaction body, RequestOptions? options = null);
        Task<MultipartUploadAttachment> MultipartUploadAttachmentFileStartAsync(global::Mezon.Net.Internal.Api.UploadAttachmentRequest body, RequestOptions? options = null);
        Task<UploadAttachment> MultipartUploadAttachmentFileFinishAsync(MultipartUploadAttachmentFinishRequest body, RequestOptions? options = null);
        Task SessionLogoutAsync(SessionLogoutRequest body, RequestOptions? options = null);
        Task<UploadAttachmentBatch> UploadBatchAttachmentFileAsync(UploadBatchAttachmentRequest body, RequestOptions? options = null);
    }
}
