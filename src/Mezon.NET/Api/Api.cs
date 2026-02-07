using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Api
{
    #region Data Contracts
    public class ClanUserListClanUser
    {
        [JsonPropertyName("clan_avatar")]
        public string ClanAvatar { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_nick")]
        public string ClanNick { get; set; }

        [JsonPropertyName("role_id")]
        public List<string>? RoleId { get; set; }

        [JsonPropertyName("user")]
        public ApiUser? User { get; set; }
    }

    public class GetPubKeysResponseUserPubKey
    {
        [JsonPropertyName("PK")]
        public ApiPubKey? PK { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class CountClanBadgeResponseBadge
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("count")]
        public int? Count { get; set; }
    }

    public class MezonChangeChannelCategoryBody
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }

    public class MezonSetChanEncryptionMethodBody
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }
    }

    public class MezonDeleteWebhookByIdBody
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }

    public class MezonUpdateAppBody
    {
        [JsonPropertyName("about")]
        public string About { get; set; }

        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }

        [JsonPropertyName("applogo")]
        public string Applogo { get; set; }

        [JsonPropertyName("appname")]
        public string Appname { get; set; }

        [JsonPropertyName("metadata")]
        public string Metadata { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    public class MezonUpdateCategoryBody
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }
    }

    public class ApiAddAppRequest
    {
        [JsonPropertyName("about_me")]
        public string AboutMe { get; set; }

        [JsonPropertyName("app_logo")]
        public string AppLogo { get; set; }

        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }

        [JsonPropertyName("appname")]
        public string Appname { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("is_shadow")]
        public bool? IsShadow { get; set; }

        [JsonPropertyName("role")]
        public int? Role { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    public enum ApiAppRole
    {
        USER_ROLE_UNKNOWN = 0,
        USER_ROLE_ADMIN = 1,
        USER_ROLE_DEVELOPER = 2,
        USER_ROLE_MAINTAINER = 3,
        USER_ROLE_READONLY = 4,
    }

    public class MezonUpdateChannelDescBody
    {
        [JsonPropertyName("age_restricted")]
        public int? AgeRestricted { get; set; }

        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }

        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("e2ee")]
        public int? E2ee { get; set; }

        [JsonPropertyName("topic")]
        public string Topic { get; set; }
    }

    public class MezonUpdateClanDescBody
    {
        [JsonPropertyName("banner")]
        public string Banner { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("is_onboarding")]
        public bool? IsOnboarding { get; set; }

        [JsonPropertyName("welcome_channel_id")]
        public string WelcomeChannelId { get; set; }

        [JsonPropertyName("onboarding_banner")]
        public string OnboardingBanner { get; set; }
    }

    public class MezonUpdateClanDescProfileBody
    {
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("nick_name")]
        public string NickName { get; set; }

        [JsonPropertyName("profile_banner")]
        public string ProfileBanner { get; set; }

        [JsonPropertyName("profile_theme")]
        public string ProfileTheme { get; set; }
    }

    public class MezonUpdateClanEmojiByIdBody
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("shortname")]
        public string Shortname { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    public class MezonUpdateClanStickerByIdBody
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("shortname")]
        public string Shortname { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    public class MezonUpdateEventBody
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_voice_id")]
        public string ChannelVoiceId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("channel_id_old")]
        public string ChannelIdOld { get; set; }

        [JsonPropertyName("repeat_type")]
        public int? RepeatType { get; set; }
    }



    public class MezonUpdateRoleDeleteBody
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }

    public class MezonUpdateSystemMessageBody
    {
        [JsonPropertyName("boost_message")]
        public string BoostMessage { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("hide_audit_log")]
        public string HideAuditLog { get; set; }

        [JsonPropertyName("setup_tips")]
        public string SetupTips { get; set; }

        [JsonPropertyName("welcome_random")]
        public string WelcomeRandom { get; set; }

        [JsonPropertyName("welcome_sticker")]
        public string WelcomeSticker { get; set; }
    }

    public class MezonUpdateUserProfileByClanBody
    {
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("nick_name")]
        public string NickName { get; set; }
    }

    public class MezonUpdateWebhookByIdBody
    {
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_id_update")]
        public string ChannelIdUpdate { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("webhook_name")]
        public string WebhookName { get; set; }
    }

    public class RoleUserListRoleUser
    {
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("lang_tag")]
        public string LangTag { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("online")]
        public bool? Online { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }

    public class UpdateClanOrderRequestClanOrder
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("order")]
        public int? Order { get; set; }
    }

    public class ApiUpdateClanOrderRequest
    {
        [JsonPropertyName("clans_order")]
        public List<UpdateClanOrderRequestClanOrder>? ClansOrder { get; set; }
    }

    public class ApiAccount
    {
        [JsonPropertyName("custom_id")]
        public string CustomId { get; set; }

        [JsonPropertyName("disable_time")]
        public string DisableTime { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("encrypt_private_key")]
        public string EncryptPrivateKey { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("splash_screen")]
        public string SplashScreen { get; set; }

        [JsonPropertyName("user")]
        public ApiUser? User { get; set; }

        [JsonPropertyName("verify_time")]
        public string VerifyTime { get; set; }

        [JsonPropertyName("wallet")]
        public double? Wallet { get; set; }
    }

    public class ApiAccountApp
    {
        [JsonPropertyName("appid")]
        public string Appid { get; set; }

        [JsonPropertyName("appname")]
        public string Appname { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("vars")]
        public Dictionary<string, string>? Vars { get; set; }
    }





    public class ApiAddFavoriteChannelRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }

    public class ApiAddFavoriteChannelResponse
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }
    }

    public class ApiAddRoleChannelDescRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("role_ids")]
        public List<string>? RoleIds { get; set; }
    }

    public class ApiAllUsersAddChannelResponse
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        [JsonPropertyName("user_ids")]
        public List<string>? UserIds { get; set; }
    }

    public class ApiAllUserClans
    {
        [JsonPropertyName("users")]
        public List<ApiUser>? Users { get; set; }
    }

    public class ApiApp
    {
        [JsonPropertyName("about")]
        public string About { get; set; }

        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }

        [JsonPropertyName("applogo")]
        public string Applogo { get; set; }

        [JsonPropertyName("appname")]
        public string Appname { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("disable_time")]
        public string DisableTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("is_shadow")]
        public bool? IsShadow { get; set; }

        [JsonPropertyName("role")]
        public int? Role { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    public class ApiAuditLog
    {
        [JsonPropertyName("action_log")]
        public string ActionLog { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("details")]
        public string Details { get; set; }

        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; }

        [JsonPropertyName("entity_name")]
        public string EntityName { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("time_log")]
        public string TimeLog { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }



    public class ApiCategoryDesc
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        [JsonPropertyName("category_order")]
        public int? CategoryOrder { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }
    }

    public class ApiCategoryDescList
    {
        [JsonPropertyName("categorydesc")]
        public List<ApiCategoryDesc>? Categorydesc { get; set; }
    }

    public class ApiCategoryOrderUpdate
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("order")]
        public int? Order { get; set; }
    }

    public class ApiChanEncryptionMethod
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; }
    }

    public class ApiChangeChannelPrivateRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_private")]
        public int? ChannelPrivate { get; set; }

        [JsonPropertyName("role_ids")]
        public List<string>? RoleIds { get; set; }

        [JsonPropertyName("user_ids")]
        public List<string>? UserIds { get; set; }
    }

    public class ApiChannelAppResponse
    {
        [JsonPropertyName("app_id")]
        public string AppId { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }
    }

    public class ApiChannelAttachment
    {
        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        [JsonPropertyName("filesize")]
        public string Filesize { get; set; }

        [JsonPropertyName("filetype")]
        public string Filetype { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("uploader")]
        public string Uploader { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }
    }

    public class ApiChannelAttachmentList
    {
        [JsonPropertyName("attachments")]
        public List<ApiChannelAttachment>? Attachments { get; set; }
    }

    public class ApiChannelCanvasDetailResponse
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("editor_id")]
        public string EditorId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("is_default")]
        public bool? IsDefault { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }

    public class ApiChannelCanvasItem
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("is_default")]
        public bool? IsDefault { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }

    public class ApiChannelCanvasListResponse
    {
        [JsonPropertyName("channel_canvases")]
        public List<ApiChannelCanvasItem>? ChannelCanvases { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("count")]
        public int? Count { get; set; }
    }

    public class ApiEditChannelCanvasRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("is_default")]
        public bool? IsDefault { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }
    }

    public class ApiEditChannelCanvasResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    public class ApiChannelDescList
    {
        [JsonPropertyName("cacheable_cursor")]
        public string CacheableCursor { get; set; }

        [JsonPropertyName("channeldesc")]
        public List<ApiChannelDescription>? Channeldesc { get; set; }

        [JsonPropertyName("next_cursor")]
        public string NextCursor { get; set; }

        [JsonPropertyName("page")]
        public int? Page { get; set; }

        [JsonPropertyName("prev_cursor")]
        public string PrevCursor { get; set; }
    }

    public class ApiAddChannelAppRequest
    {
        [JsonPropertyName("app_url")]
        public string AppUrl { get; set; }

        [JsonPropertyName("appname")]
        public string Appname { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("role")]
        public int? Role { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    public class ApiChannelDescription
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("age_restricted")]
        public int? AgeRestricted { get; set; }

        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        [JsonPropertyName("channel_avatar")]
        public List<string>? ChannelAvatar { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("channel_private")]
        public int? ChannelPrivate { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("count_mess_unread")]
        public int? CountMessUnread { get; set; }

        [JsonPropertyName("create_time_seconds")]
        public long? CreateTimeSeconds { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("creator_name")]
        public string CreatorName { get; set; }

        [JsonPropertyName("e2ee")]
        public int? E2ee { get; set; }

        [JsonPropertyName("is_mute")]
        public bool? IsMute { get; set; }

        [JsonPropertyName("last_pin_message")]
        public string LastPinMessage { get; set; }

        [JsonPropertyName("last_seen_message")]
        public ApiChannelMessageHeader? LastSeenMessage { get; set; }

        [JsonPropertyName("last_sent_message")]
        public ApiChannelMessageHeader? LastSentMessage { get; set; }

        [JsonPropertyName("meeting_code")]
        public string MeetingCode { get; set; }

        [JsonPropertyName("meeting_uri")]
        public string MeetingUri { get; set; }

        [JsonPropertyName("parent_id")]
        public string ParentId { get; set; }

        [JsonPropertyName("is_online")]
        public List<bool>? IsOnline { get; set; }

        [JsonPropertyName("topic")]
        public string Topic { get; set; }

        [JsonPropertyName("type")]
        public int? Type { get; set; }

        [JsonPropertyName("update_time_seconds")]
        public long? UpdateTimeSeconds { get; set; }

        [JsonPropertyName("user_id")]
        public List<string>? UserId { get; set; }

        [JsonPropertyName("usernames")]
        public List<string>? Usernames { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("metadata")]
        public List<string>? Metadata { get; set; }

        [JsonPropertyName("about_me")]
        public List<string>? AboutMe { get; set; }

        [JsonPropertyName("display_names")]
        public List<string>? DisplayNames { get; set; }

        [JsonPropertyName("app_id")]
        public string AppId { get; set; }
    }

    public class ApiChannelMessage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("attachments")]
        public string Attachments { get; set; }

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; } = "";

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; } = "";

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_logo")]
        public string ClanLogo { get; set; }

        [JsonPropertyName("clan_nick")]
        public string ClanNick { get; set; }

        [JsonPropertyName("clan_avatar")]
        public string ClanAvatar { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("create_time_seconds")]
        public long? CreateTimeSeconds { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("mentions")]
        public string Mentions { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; } = "";

        [JsonPropertyName("reactions")]
        public string Reactions { get; set; }

        [JsonPropertyName("referenced_message")]
        public string ReferencedMessage { get; set; }

        [JsonPropertyName("references")]
        public string References { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; } = "";

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("update_time_seconds")]
        public long? UpdateTimeSeconds { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("mode")]
        public int? Mode { get; set; }

        [JsonPropertyName("hide_editted")]
        public bool? HideEditted { get; set; }

        [JsonPropertyName("topic_id")]
        public string TopicId { get; set; }
    }

    public class ApiChannelMessageHeader
    {
        [JsonPropertyName("attachment")]
        public string Attachment { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("mention")]
        public string Mention { get; set; }

        [JsonPropertyName("reaction")]
        public string Reaction { get; set; }

        [JsonPropertyName("reference")]
        public string Reference { get; set; }

        [JsonPropertyName("repliers")]
        public List<string>? Repliers { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("timestamp_seconds")]
        public long? TimestampSeconds { get; set; }
    }

    public class ApiChannelMessageList
    {
        [JsonPropertyName("last_seen_message")]
        public ApiChannelMessageHeader? LastSeenMessage { get; set; }

        [JsonPropertyName("last_sent_message")]
        public ApiChannelMessageHeader? LastSentMessage { get; set; }

        [JsonPropertyName("messages")]
        public List<ApiChannelMessage>? Messages { get; set; }
    }

    public class ApiChannelSettingItem
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("channel_private")]
        public int? ChannelPrivate { get; set; }

        [JsonPropertyName("channel_type")]
        public int? ChannelType { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("last_sent_message")]
        public ApiChannelMessageHeader? LastSentMessage { get; set; }

        [JsonPropertyName("meeting_code")]
        public string MeetingCode { get; set; }

        [JsonPropertyName("message_count")]
        public string MessageCount { get; set; }

        [JsonPropertyName("parent_id")]
        public string ParentId { get; set; }

        [JsonPropertyName("user_ids")]
        public List<string>? UserIds { get; set; }
    }

    public class ApiChannelSettingListResponse
    {
        [JsonPropertyName("channel_count")]
        public int? ChannelCount { get; set; }

        [JsonPropertyName("channel_setting_list")]
        public List<ApiChannelSettingItem>? ChannelSettingList { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("thread_count")]
        public int? ThreadCount { get; set; }
    }

    public class ApiChannelUserList
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        //[JsonPropertyName("channel_users")]
        //public List<ChannelUserListChannelUser>? ChannelUsers { get; set; }

        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }

    public class ApiCheckDuplicateClanNameResponse
    {
        [JsonPropertyName("is_duplicate")]
        public bool? IsDuplicate { get; set; }
    }

    public class ApiClanDesc
    {
        [JsonPropertyName("banner")]
        public string Banner { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("badge_count")]
        public int? BadgeCount { get; set; }

        [JsonPropertyName("is_onboarding")]
        public bool? IsOnboarding { get; set; }

        [JsonPropertyName("welcome_channel_id")]
        public string WelcomeChannelId { get; set; }

        [JsonPropertyName("onboarding_banner")]
        public string OnboardingBanner { get; set; }
    }

    public class ApiClanDescList
    {
        [JsonPropertyName("clandesc")]
        public List<ApiClanDesc>? Clandesc { get; set; }
    }

    public class ApiClanDescProfile
    {
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("nick_name")]
        public string NickName { get; set; }

        [JsonPropertyName("profile_banner")]
        public string ProfileBanner { get; set; }

        [JsonPropertyName("profile_theme")]
        public string ProfileTheme { get; set; }
    }

    public class ApiClanEmoji
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("shortname")]
        public string Shortname { get; set; }

        [JsonPropertyName("src")]
        public string Src { get; set; }
    }

    public class ApiClanEmojiCreateRequest
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("shortname")]
        public string Shortname { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    public class ApiClanProfile
    {
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("nick_name")]
        public string NickName { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class ApiClanSticker
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("shortname")]
        public string Shortname { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    public class ApiClanStickerAddRequest
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("shortname")]
        public string Shortname { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    public class ApiClanUserList
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_users")]
        public List<ClanUserListClanUser>? ClanUsers { get; set; }

        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }

    public class ApiConfirmLoginRequest
    {
        [JsonPropertyName("is_remember")]
        public bool? IsRemember { get; set; }

        [JsonPropertyName("login_id")]
        public string LoginId { get; set; }
    }



    public class ApiCreateCategoryDescRequest
    {
        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }

    public class ApiCreateChannelDescRequest
    {
        [JsonPropertyName("app_id")]
        public string AppId { get; set; }

        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("channel_private")]
        public int? ChannelPrivate { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("parent_id")]
        public string ParentId { get; set; }

        [JsonPropertyName("type")]
        public int? Type { get; set; }

        [JsonPropertyName("user_ids")]
        public List<string>? UserIds { get; set; }
    }

    public class ApiCreateClanDescRequest
    {
        [JsonPropertyName("banner")]
        public string Banner { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }
    }

    public class ApiCreateEventRequest
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("channel_voice_id")]
        public string ChannelVoiceId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("action")]
        public int? Action { get; set; }

        [JsonPropertyName("event_status")]
        public int? EventStatus { get; set; }

        [JsonPropertyName("repeat_type")]
        public int? RepeatType { get; set; }

        [JsonPropertyName("creator_id")]
        public int? CreatorIdNum { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("is_private")]
        public bool? IsPrivate { get; set; }

        [JsonPropertyName("meet_room")]
        public ApiGenerateMezonMeetResponse? MeetRoom { get; set; }
    }

    public class ApiUpdateEventRequest
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }



    public class ApiDeleteChannelDescRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }
    }

    public class ApiDeleteEventRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("event_label")]
        public string EventLabel { get; set; }
    }

    public class ApiDeleteRoleRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("role_id")]
        public string RoleId { get; set; }

        [JsonPropertyName("role_label")]
        public string RoleLabel { get; set; }
    }

    public class ApiDeleteStorageObjectId
    {
        [JsonPropertyName("collection")]
        public string Collection { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }

    public class ApiDeleteStorageObjectsRequest
    {
        [JsonPropertyName("object_ids")]
        public List<ApiDeleteStorageObjectId>? ObjectIds { get; set; }
    }

    public class ApiEvent
    {
        [JsonPropertyName("external")]
        public bool? External { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, string>? Properties { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
    }

    public class ApiEmojiListedResponse
    {
        [JsonPropertyName("emoji_list")]
        public List<ApiClanEmoji>? EmojiList { get; set; }
    }

    public class ApiEmojiRecent
    {
        [JsonPropertyName("emoji_recents_id")]
        public string EmojiRecentsId { get; set; }

        [JsonPropertyName("emoji_id")]
        public string EmojiId { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }
    }

    public class ApiEventList
    {
        [JsonPropertyName("events")]
        public List<ApiEventManagement>? Events { get; set; }
    }

    public class ApiEventManagement
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("channel_voice_id")]
        public string ChannelVoiceId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("max_permission")]
        public int? MaxPermission { get; set; }

        [JsonPropertyName("start_event")]
        public int? StartEvent { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("user_ids")]
        public List<string>? UserIds { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("event_status")]
        public int? EventStatus { get; set; }

        [JsonPropertyName("repeat_type")]
        public int? RepeatType { get; set; }

        [JsonPropertyName("is_private")]
        public bool? IsPrivate { get; set; }

        [JsonPropertyName("meet_room")]
        public ApiGenerateMezonMeetResponse? MeetRoom { get; set; }
    }

    public class ApiListFavoriteChannelResponse
    {
        [JsonPropertyName("channel_ids")]
        public List<string>? ChannelIds { get; set; }
    }

    public class ApiFilterParam
    {
        [JsonPropertyName("field_name")]
        public string FieldName { get; set; }

        [JsonPropertyName("field_value")]
        public string FieldValue { get; set; }
    }

    public class ApiFriend
    {
        [JsonPropertyName("state")]
        public int? State { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("user")]
        public ApiUser? User { get; set; }
    }

    public class ApiFriendList
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }

        [JsonPropertyName("friends")]
        public List<ApiFriend>? Friends { get; set; }
    }

    public class ApiGetKeyServerResp
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class ApiGenerateMezonMeetResponse
    {
        [JsonPropertyName("meet_id")]
        public string MeetId { get; set; }
        [JsonPropertyName("room_name")]
        public string RoomName { get; set; }
        [JsonPropertyName("external_link")]
        public string ExternalLink { get; set; }
        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }
        [JsonPropertyName("event_id")]
        public string EventId { get; set; }
    }

    public class ApiGenerateMeetTokenExternalResponse
    {
        [JsonPropertyName("guest_user_id")]
        public string GuestUserId { get; set; }
        [JsonPropertyName("token")]
        public string Token { get; set; }
        [JsonPropertyName("guest_access_token")]
        public string GuestAccessToken { get; set; }
    }

    public class ApiGetPubKeysResponse
    {
        [JsonPropertyName("pub_keys")]
        public List<GetPubKeysResponseUserPubKey>? PubKeys { get; set; }
    }

    public class ApiGiveCoffeeEvent
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("message_ref_id")]
        public string MessageRefId { get; set; }

        [JsonPropertyName("receiver_id")]
        public string ReceiverId { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("token_count")]
        public int? TokenCount { get; set; }
    }

    public class ApiHashtagDm
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("channel_private")]
        public int? ChannelPrivate { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("meeting_code")]
        public string MeetingCode { get; set; }

        [JsonPropertyName("parent_id")]
        public string ParentId { get; set; }

        [JsonPropertyName("type")]
        public int? Type { get; set; }
    }

    public class ApiHashtagDmList
    {
        [JsonPropertyName("hashtag_dm")]
        public List<ApiHashtagDm>? HashtagDm { get; set; }
    }

    public class ApiInviteUserRes
    {
        [JsonPropertyName("channel_desc")]
        public ApiChannelDescription? ChannelDesc { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("user_joined")]
        public bool? UserJoined { get; set; }

        [JsonPropertyName("expiry_time")]
        public string ExpiryTime { get; set; }

        [JsonPropertyName("clan_logo")]
        public string ClanLogo { get; set; }

        [JsonPropertyName("member_count")]
        public int? MemberCount { get; set; }
    }

    public class ApiLinkInviteUser
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("expiry_time")]
        public string ExpiryTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("invite_link")]
        public string InviteLink { get; set; }
    }

    public class ApiLinkInviteUserRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("expiry_time")]
        public int? ExpiryTime { get; set; }
    }

    public class ApiNotifiReactMessage
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class ApiMessage2InboxRequest
    {
        [JsonPropertyName("attachments")]
        public string Attachments { get; set; }

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("mentions")]
        public string Mentions { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("reactions")]
        public string Reactions { get; set; }

        [JsonPropertyName("references")]
        public string References { get; set; }
    }

    public class ApiMessageAttachment
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        [JsonPropertyName("filetype")]
        public string Filetype { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("size")]
        public int? Size { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("thumbnail")]
        public string Thumbnail { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("mode")]
        public int? Mode { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }
    }

    public class ApiMessageDeleted
    {
        [JsonPropertyName("deletor")]
        public string Deletor { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }
    }

    public class ApiListUserActivity
    {
        [JsonPropertyName("activities")]
        public List<ApiUserActivity>? Activities { get; set; }
    }

    public class ApiLoginIDResponse
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("create_time_second")]
        public string CreateTimeSecond { get; set; }

        [JsonPropertyName("login_id")]
        public string LoginId { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }

    public class ApiMarkAsReadRequest
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }

    public class ApiMessageMention
    {
        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("role_id")]
        public string RoleId { get; set; }

        [JsonPropertyName("rolename")]
        public string Rolename { get; set; }

        [JsonPropertyName("s")]
        public int? S { get; set; }

        [JsonPropertyName("e")]
        public int? E { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("mode")]
        public int? Mode { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }
    }

    public class ApiLoginRequest
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }
    }

    public class ApiMessageReaction
    {
        [JsonPropertyName("action")]
        public bool? Action { get; set; }

        [JsonPropertyName("emoji_id")]
        public string EmojiId { get; set; } = "";

        [JsonPropertyName("emoji")]
        public string Emoji { get; set; } = "";

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("sender_name")]
        public string SenderName { get; set; }

        [JsonPropertyName("sender_avatar")]
        public string SenderAvatar { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; } = "";

        [JsonPropertyName("mode")]
        public int Mode { get; set; }

        [JsonPropertyName("is_public")]
        public bool IsPublic { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; } = "";

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; } = "";

        [JsonPropertyName("topic_id")]
        public string TopicId { get; set; }

        [JsonPropertyName("emoji_recent_id")]
        public string EmojiRecentId { get; set; }
    }

    public class ApiListChannelAppsResponse
    {
        [JsonPropertyName("channel_apps")]
        public List<ApiChannelAppResponse>? ChannelApps { get; set; }
    }

    public class ApiListStreamingChannelsResponse
    {
        [JsonPropertyName("streaming_channels")]
        public List<ApiStreamingChannelResponse>? StreamingChannels { get; set; }
    }

    public class ApiMezonOauthClient
    {
        [JsonPropertyName("access_token_strategy")]
        public string AccessTokenStrategy { get; set; }
        [JsonPropertyName("allowed_cors_origins")]
        public List<string>? AllowedCorsOrigins { get; set; }
        [JsonPropertyName("audience")]
        public List<string>? Audience { get; set; }
        [JsonPropertyName("authorization_code_grant_access_token_lifespan")]
        public string AuthorizationCodeGrantAccessTokenLifespan { get; set; }
        [JsonPropertyName("authorization_code_grant_id_token_lifespan")]
        public string AuthorizationCodeGrantIdTokenLifespan { get; set; }
        [JsonPropertyName("authorization_code_grant_refresh_token_lifespan")]
        public string AuthorizationCodeGrantRefreshTokenLifespan { get; set; }
        [JsonPropertyName("backchannel_logout_session_required")]
        public bool? BackchannelLogoutSessionRequired { get; set; }
        [JsonPropertyName("backchannel_logout_uri")]
        public string BackchannelLogoutUri { get; set; }
        [JsonPropertyName("client_credentials_grant_access_token_lifespan")]
        public string ClientCredentialsGrantAccessTokenLifespan { get; set; }
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }
        [JsonPropertyName("client_name")]
        public string ClientName { get; set; }
        [JsonPropertyName("client_secret")]
        public string ClientSecret { get; set; }
        [JsonPropertyName("client_secret_expires_at")]
        public long? ClientSecretExpiresAt { get; set; }
        [JsonPropertyName("client_uri")]
        public string ClientUri { get; set; }
        [JsonPropertyName("contacts")]
        public List<string>? Contacts { get; set; }
        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }
        [JsonPropertyName("frontchannel_logout_session_required")]
        public bool? FrontchannelLogoutSessionRequired { get; set; }
        [JsonPropertyName("frontchannel_logout_uri")]
        public string FrontchannelLogoutUri { get; set; }
        [JsonPropertyName("grant_types")]
        public List<string>? GrantTypes { get; set; }
        [JsonPropertyName("implicit_grant_access_token_lifespan")]
        public string ImplicitGrantAccessTokenLifespan { get; set; }
        [JsonPropertyName("implicit_grant_id_token_lifespan")]
        public string ImplicitGrantIdTokenLifespan { get; set; }
        [JsonPropertyName("jwks")]
        public List<string>? Jwks { get; set; }
        [JsonPropertyName("jwks_uri")]
        public string JwksUri { get; set; }
        [JsonPropertyName("jwt_bearer_grant_access_token_lifespan")]
        public string JwtBearerGrantAccessTokenLifespan { get; set; }
        [JsonPropertyName("logo_uri")]
        public string LogoUri { get; set; }
        [JsonPropertyName("owner")]
        public string Owner { get; set; }
        [JsonPropertyName("policy_uri")]
        public string PolicyUri { get; set; }
        [JsonPropertyName("post_logout_redirect_uris")]
        public List<string>? PostLogoutRedirectUris { get; set; }
        [JsonPropertyName("redirect_uris")]
        public List<string>? RedirectUris { get; set; }
        [JsonPropertyName("refresh_token_grant_access_token_lifespan")]
        public string RefreshTokenGrantAccessTokenLifespan { get; set; }
        [JsonPropertyName("refresh_token_grant_id_token_lifespan")]
        public string RefreshTokenGrantIdTokenLifespan { get; set; }
        [JsonPropertyName("refresh_token_grant_refresh_token_lifespan")]
        public string RefreshTokenGrantRefreshTokenLifespan { get; set; }
        [JsonPropertyName("registration_access_token")]
        public string RegistrationAccessToken { get; set; }
        [JsonPropertyName("registration_client_uri")]
        public string RegistrationClientUri { get; set; }
        [JsonPropertyName("request_object_signing_alg")]
        public string RequestObjectSigningAlg { get; set; }
        [JsonPropertyName("request_uris")]
        public List<string>? RequestUris { get; set; }
        [JsonPropertyName("response_types")]
        public List<string>? ResponseTypes { get; set; }
        [JsonPropertyName("scope")]
        public string Scope { get; set; }
        [JsonPropertyName("sector_identifier_uri")]
        public string SectorIdentifierUri { get; set; }
        [JsonPropertyName("skip_consent")]
        public bool? SkipConsent { get; set; }
        [JsonPropertyName("skip_logout_consent")]
        public bool? SkipLogoutConsent { get; set; }
        [JsonPropertyName("subject_type")]
        public string SubjectType { get; set; }
        [JsonPropertyName("token_endpoint_auth_method")]
        public string TokenEndpointAuthMethod { get; set; }
        [JsonPropertyName("token_endpoint_auth_signing_alg")]
        public string TokenEndpointAuthSigningAlg { get; set; }
        [JsonPropertyName("tos_uri")]
        public string TosUri { get; set; }
        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; }
        [JsonPropertyName("userinfo_signed_response_alg")]
        public string UserinfoSignedResponseAlg { get; set; }
    }

    public class ApiMezonOauthClientList
    {
        [JsonPropertyName("list_mezon_oauth_client")]
        public List<ApiMezonOauthClient>? ListMezonOauthClient { get; set; }
    }

    public class ApiMessageRef
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("message_ref_id")]
        public string MessageRefId { get; set; }

        [JsonPropertyName("ref_type")]
        public int? RefType { get; set; }

        [JsonPropertyName("message_sender_id")]
        public string MessageSenderId { get; set; }

        [JsonPropertyName("message_sender_username")]
        public string MessageSenderUsername { get; set; }

        [JsonPropertyName("mesages_sender_avatar")]
        public string MesagesSenderAvatar { get; set; }

        [JsonPropertyName("message_sender_clan_nick")]
        public string MessageSenderClanNick { get; set; }

        [JsonPropertyName("message_sender_display_name")]
        public string MessageSenderDisplayName { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("has_attachment")]
        public bool HasAttachment { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; } = "";

        [JsonPropertyName("mode")]
        public int Mode { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; } = "";
    }

    public class ApiNotificationChannel
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }
    }

    public class ApiNotificationChannelCategorySetting
    {
        [JsonPropertyName("action")]
        public int? Action { get; set; }

        [JsonPropertyName("channel_category_label")]
        public string ChannelCategoryLabel { get; set; }

        [JsonPropertyName("channel_category_title")]
        public string ChannelCategoryTitle { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("notification_setting_type")]
        public int? NotificationSettingType { get; set; }
    }

    public class ApiNotificationChannelCategorySettingList
    {
        [JsonPropertyName("notification_channel_category_settings_list")]
        public List<ApiNotificationChannelCategorySetting>? NotificationChannelCategorySettingsList { get; set; }
    }

    public class ApiNotificationList
    {
        [JsonPropertyName("cacheable_cursor")]
        public string CacheableCursor { get; set; }

        //[JsonPropertyName("notifications")]
        //public List<ApiNotification>? Notifications { get; set; }
    }

    public class ApiNotificationSetting
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("notification_setting_type")]
        public int? NotificationSettingType { get; set; }
    }

    public class ApiNotificationUserChannel
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("notification_setting_type")]
        public int? NotificationSettingType { get; set; }

        [JsonPropertyName("time_mute")]
        public string TimeMute { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }
    }

    public class ApiStreamHttpCallbackRequest
    {
        [JsonPropertyName("action")]
        public string Action { get; set; }

        [JsonPropertyName("app")]
        public string App { get; set; }

        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }

        [JsonPropertyName("ip")]
        public string Ip { get; set; }

        [JsonPropertyName("page_url")]
        public string PageUrl { get; set; }

        [JsonPropertyName("param")]
        public string Param { get; set; }

        [JsonPropertyName("server_id")]
        public string ServerId { get; set; }

        [JsonPropertyName("service_id")]
        public string ServiceId { get; set; }

        [JsonPropertyName("stream")]
        public string Stream { get; set; }

        [JsonPropertyName("stream_id")]
        public string StreamId { get; set; }

        [JsonPropertyName("stream_url")]
        public string StreamUrl { get; set; }

        [JsonPropertyName("tc_url")]
        public string TcUrl { get; set; }

        [JsonPropertyName("vhost")]
        public string Vhost { get; set; }
    }

    public class ApiStreamHttpCallbackResponse
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; }
    }

    public class ApiPermission
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }

        [JsonPropertyName("scope")]
        public int? Scope { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }

    public class ApiPermissionList
    {
        [JsonPropertyName("max_level_permission")]
        public int? MaxLevelPermission { get; set; }

        [JsonPropertyName("permissions")]
        public List<ApiPermission>? Permissions { get; set; }
    }

    public class ApiPermissionRoleChannel
    {
        [JsonPropertyName("active")]
        public bool? Active { get; set; }

        [JsonPropertyName("permission_id")]
        public string PermissionId { get; set; }
    }

    public class ApiPermissionRoleChannelListEventResponse
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("permission_role_channel")]
        public List<ApiPermissionRoleChannel>? PermissionRoleChannel { get; set; }

        [JsonPropertyName("role_id")]
        public string RoleId { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class ApiPermissionUpdate
    {
        [JsonPropertyName("permission_id")]
        public string PermissionId { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("type")]
        public int? Type { get; set; }
    }

    public class ApiPinMessage
    {
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("create_time_seconds")]
        public long? CreateTimeSeconds { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("attachment")]
        public string Attachment { get; set; }
    }

    public class ApiPinMessageRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }
    }

    public class ApiPinMessagesList
    {
        [JsonPropertyName("pin_messages_list")]
        public List<ApiPinMessage>? PinMessagesList { get; set; }
    }

    public class ApiPubKey
    {
        [JsonPropertyName("encr")]
        public string Encr { get; set; }

        [JsonPropertyName("sign")]
        public string Sign { get; set; }
    }

    public class ApiPushPubKeyRequest
    {
        [JsonPropertyName("PK")]
        public ApiPubKey? PK { get; set; }
    }

    public class ApiRegistFcmDeviceTokenResponse
    {
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    public class ApiRegisterStreamingChannelRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }

    public class ApiRegisterStreamingChannelResponse
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("streaming_url")]
        public string StreamingUrl { get; set; }
    }

    public class ApiReadStorageObjectId
    {
        [JsonPropertyName("collection")]
        public string Collection { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class ApiReadStorageObjectsRequest
    {
        [JsonPropertyName("object_ids")]
        public List<ApiReadStorageObjectId>? ObjectIds { get; set; }
    }

    public class ApiRegistrationEmailRequest
    {
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("dob")]
        public string Dob { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("vars")]
        public Dictionary<string, string>? Vars { get; set; }
    }

    public class ApiUpdateRoleOrderRequest
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("roles")]
        public List<ApiRoleOrderUpdate>? Roles { get; set; }
    }

    public class ApiRoleOrderUpdate
    {
        [JsonPropertyName("order")]
        public int? Order { get; set; }

        [JsonPropertyName("role_id")]
        public string RoleId { get; set; }
    }





    public class ApiRoleUserList
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }

        [JsonPropertyName("role_users")]
        public List<RoleUserListRoleUser>? RoleUsers { get; set; }
    }

    public class ApiRpc
    {
        [JsonPropertyName("http_key")]
        public string HttpKey { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("payload")]
        public string Payload { get; set; }
    }

    public class ApiSdTopic
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("last_sent_message")]
        public ApiChannelMessageHeader? LastSentMessage { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("message")]
        public ApiChannelMessage? Message { get; set; }
    }

    public class ApiSdTopicList
    {
        [JsonPropertyName("count")]
        public int? Count { get; set; }

        [JsonPropertyName("topics")]
        public List<ApiSdTopic>? Topics { get; set; }
    }

    public class ApiSdTopicRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }
    }

    public class ApiSearchMessageDocument
    {
        [JsonPropertyName("attachments")]
        public List<ApiMessageAttachment>? Attachments { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("channel_label")]
        public string ChannelLabel { get; set; }

        [JsonPropertyName("channel_type")]
        public int? ChannelType { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("mentions")]
        public string Mentions { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("reactions")]
        public string Reactions { get; set; }

        [JsonPropertyName("references")]
        public string References { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }

    public class ApiSearchMessageRequest
    {
        [JsonPropertyName("filters")]
        public List<ApiFilterParam>? Filters { get; set; }

        [JsonPropertyName("from")]
        public int? From { get; set; }

        [JsonPropertyName("size")]
        public int? Size { get; set; }

        [JsonPropertyName("sorts")]
        public List<ApiSortParam>? Sorts { get; set; }
    }

    public class ApiSearchMessageResponse
    {
        [JsonPropertyName("messages")]
        public List<ApiSearchMessageDocument>? Messages { get; set; }

        [JsonPropertyName("total")]
        public int? Total { get; set; }
    }

    public class ApiSession
    {
        [JsonPropertyName("created")]
        public bool? Created { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("is_remember")]
        public bool? IsRemember { get; set; }

        [JsonPropertyName("api_url")]
        public string ApiUrl { get; set; }
    }

    public class ApiSessionLogoutRequest
    {
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }
    }

    public class ApiSessionRefreshRequest
    {
        [JsonPropertyName("is_remember")]
        public bool? IsRemember { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("vars")]
        public Dictionary<string, string>? Vars { get; set; }
    }

    public class ApiSetDefaultNotificationRequest
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("notification_type")]
        public int? NotificationType { get; set; }
    }

    public class ApiSetMuteNotificationRequest
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("notification_type")]
        public int? NotificationType { get; set; }
    }

    public class ApiSetNotificationRequest
    {
        [JsonPropertyName("channel_category_id")]
        public string ChannelCategoryId { get; set; }

        [JsonPropertyName("notification_type")]
        public int? NotificationType { get; set; }

        [JsonPropertyName("time_mute")]
        public string TimeMute { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }

    public class ApiSortParam
    {
        [JsonPropertyName("field_name")]
        public string FieldName { get; set; }

        [JsonPropertyName("order")]
        public string Order { get; set; }
    }

    public class ApiStickerListedResponse
    {
        [JsonPropertyName("stickers")]
        public List<ApiClanSticker>? Stickers { get; set; }
    }

    public class ApiStreamingChannelResponse
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("is_streaming")]
        public bool? IsStreaming { get; set; }

        [JsonPropertyName("streaming_url")]
        public string StreamingUrl { get; set; }
    }

    public class ApiStreamingChannelUser
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("participant")]
        public string Participant { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class ApiStreamingChannelUserList
    {
        [JsonPropertyName("streaming_channel_users")]
        public List<ApiStreamingChannelUser>? StreamingChannelUsers { get; set; }
    }

    public class ApiSystemMessage
    {
        [JsonPropertyName("boost_message")]
        public string BoostMessage { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("hide_audit_log")]
        public string HideAuditLog { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("setup_tips")]
        public string SetupTips { get; set; }

        [JsonPropertyName("welcome_random")]
        public string WelcomeRandom { get; set; }

        [JsonPropertyName("welcome_sticker")]
        public string WelcomeSticker { get; set; }
    }

    public class ApiSystemMessageRequest
    {
        [JsonPropertyName("boost_message")]
        public string BoostMessage { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("hide_audit_log")]
        public string HideAuditLog { get; set; }

        [JsonPropertyName("setup_tips")]
        public string SetupTips { get; set; }

        [JsonPropertyName("welcome_random")]
        public string WelcomeRandom { get; set; }

        [JsonPropertyName("welcome_sticker")]
        public string WelcomeSticker { get; set; }
    }

    public class ApiSystemMessagesList
    {
        [JsonPropertyName("system_messages_list")]
        public List<ApiSystemMessage>? SystemMessagesList { get; set; }
    }

    public class ApiTokenSentEvent
    {
        [JsonPropertyName("amount")]
        public double? Amount { get; set; }

        [JsonPropertyName("note")]
        public string Note { get; set; }

        [JsonPropertyName("receiver_id")]
        public string ReceiverId { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("sender_name")]
        public string SenderName { get; set; }

        [JsonPropertyName("extra_attribute")]
        public string ExtraAttribute { get; set; }

        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; }
    }

    public class ApiTransactionDetail
    {
        [JsonPropertyName("amount")]
        public double? Amount { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("receiver_id")]
        public string ReceiverId { get; set; }

        [JsonPropertyName("receiver_username")]
        public string ReceiverUsername { get; set; }

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; }

        [JsonPropertyName("sender_username")]
        public string SenderUsername { get; set; }

        [JsonPropertyName("metadata")]
        public string Metadata { get; set; }

        [JsonPropertyName("trans_id")]
        public string TransId { get; set; }
    }

    public class ApiUpdateAccountRequest
    {
        [JsonPropertyName("about_me")]
        public string AboutMe { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("dob")]
        public string Dob { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("encrypt_private_key")]
        public string EncryptPrivateKey { get; set; }

        [JsonPropertyName("lang_tag")]
        public string LangTag { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("logo")]
        public string Logo { get; set; }

        [JsonPropertyName("splash_screen")]
        public string SplashScreen { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }

    public class ApiUpdateCategoryDescRequest
    {
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; }

        [JsonPropertyName("ClanId")]
        public string ClanId { get; set; } = "";
    }

    public class ApiUpdateCategoryOrderRequest
    {
        [JsonPropertyName("categories")]
        public List<ApiCategoryOrderUpdate>? Categories { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }
    }

    public class ApiUpdateRoleChannelRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; } = "";

        [JsonPropertyName("max_permission_id")]
        public string MaxPermissionId { get; set; } = "";

        [JsonPropertyName("permission_update")]
        public List<ApiPermissionUpdate>? PermissionUpdate { get; set; }

        [JsonPropertyName("role_id")]
        public string RoleId { get; set; }

        [JsonPropertyName("role_label")]
        public string RoleLabel { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class ApiUpdateUsersRequest
    {
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }
    }





    public class ApiUser
    {
        [JsonPropertyName("about_me")]
        public string AboutMe { get; set; }

        [JsonPropertyName("apple_id")]
        public string AppleId { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("dob")]
        public string Dob { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("edge_count")]
        public int? EdgeCount { get; set; }

        [JsonPropertyName("facebook_id")]
        public string FacebookId { get; set; }

        [JsonPropertyName("gamecenter_id")]
        public string GamecenterId { get; set; }

        [JsonPropertyName("google_id")]
        public string GoogleId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("is_mobile")]
        public bool? IsMobile { get; set; }

        [JsonPropertyName("join_time")]
        public string JoinTime { get; set; }

        [JsonPropertyName("lang_tag")]
        public string LangTag { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("metadata")]
        public string Metadata { get; set; }

        [JsonPropertyName("online")]
        public bool? Online { get; set; }

        [JsonPropertyName("steam_id")]
        public string SteamId { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }

    public class ApiUserActivity
    {
        [JsonPropertyName("activity_description")]
        public string ActivityDescription { get; set; }

        [JsonPropertyName("activity_name")]
        public string ActivityName { get; set; }

        [JsonPropertyName("activity_type")]
        public int? ActivityType { get; set; }

        [JsonPropertyName("application_id")]
        public string ApplicationId { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class ApiUserPermissionInChannelListResponse
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("permissions")]
        public ApiPermissionList? Permissions { get; set; }
    }





    public class ApiUsers
    {
        [JsonPropertyName("users")]
        public List<ApiUser>? Users { get; set; }
    }

    public class ApiVoiceChannelUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("participant")]
        public string Participant { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class ApiVoiceChannelUserList
    {
        [JsonPropertyName("voice_channel_users")]
        public List<ApiVoiceChannelUser>? VoiceChannelUsers { get; set; }
    }

    public class ApiWalletLedger
    {
        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("value")]
        public double? Value { get; set; }
    }

    public class ApiWalletLedgerList
    {
        [JsonPropertyName("count")]
        public int? Count { get; set; }

        [JsonPropertyName("wallet_ledger")]
        public List<ApiWalletLedger>? WalletLedger { get; set; }
    }

    public class ApiWebhook
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("webhook_name")]
        public string WebhookName { get; set; }
    }

    public class ApiWebhookCreateRequest
    {
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("webhook_name")]
        public string WebhookName { get; set; }
    }

    public class ApiWebhookGenerateResponse
    {
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("hook_name")]
        public string HookName { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class ApiWebhookListResponse
    {
        [JsonPropertyName("webhooks")]
        public List<ApiWebhook>? Webhooks { get; set; }
    }

    public class ApiWithdrawTokenRequest
    {
        [JsonPropertyName("amount")]
        public double? Amount { get; set; }
    }

    public class MezonapiEmojiRecentList
    {
        [JsonPropertyName("emoji_recents")]
        public List<ApiEmojiRecent>? EmojiRecents { get; set; }
    }

    public class MezonapiEvent
    {
        [JsonPropertyName("external")]
        public bool? External { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, string>? Properties { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
    }

    public class MezonapiListAuditLog
    {
        [JsonPropertyName("date_log")]
        public string DateLog { get; set; }

        [JsonPropertyName("logs")]
        public List<ApiAuditLog>? Logs { get; set; }

        [JsonPropertyName("total_count")]
        public int? TotalCount { get; set; }
    }

    public class ProtobufAny
    {
        [JsonPropertyName("type_url")]
        public string TypeUrl { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    public class RpcStatus
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("details")]
        public List<ProtobufAny>? Details { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    public class ApiListOnboardingResponse
    {
        [JsonPropertyName("list_onboarding")]
        public List<ApiOnboardingItem>? ListOnboarding { get; set; }
    }

    public class OnboardingAnswer
    {
        [JsonPropertyName("emoji")]
        public string Emoji { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; }
    }

    public class ApiOnboardingContent
    {
        [JsonPropertyName("answers")]
        public List<OnboardingAnswer>? Answers { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("guide_type")]
        public int? GuideType { get; set; }

        [JsonPropertyName("task_type")]
        public int? TaskType { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; }
    }

    public class MezonUpdateOnboardingBody
    {
        [JsonPropertyName("answers")]
        public List<OnboardingAnswer>? Answers { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("task_type")]
        public int? TaskType { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; }
    }

    public class ApiCreateOnboardingRequest
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("contents")]
        public List<ApiOnboardingContent>? Contents { get; set; }
    }

    public class ApiOnboardingItem
    {
        [JsonPropertyName("answers")]
        public List<OnboardingAnswer>? Answers { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("guide_type")]
        public int? GuideType { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("task_type")]
        public int? TaskType { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; }
    }

    public class MezonUpdateClanWebhookByIdBody
    {
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("reset_token")]
        public bool? ResetToken { get; set; }

        [JsonPropertyName("webhook_name")]
        public string WebhookName { get; set; }
    }

    public class ApiClanWebhook
    {
        [JsonPropertyName("active")]
        public int? Active { get; set; }

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("creator_id")]
        public string CreatorId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("update_time")]
        public string UpdateTime { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("webhook_name")]
        public string WebhookName { get; set; }
    }

    public class ApiGenerateClanWebhookRequest
    {
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("webhook_name")]
        public string WebhookName { get; set; }
    }

    public class ApiGenerateClanWebhookResponse
    {
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("webhook_name")]
        public string WebhookName { get; set; }
    }

    public class ApiListClanWebhookResponse
    {
        [JsonPropertyName("list_clan_webhooks")]
        public List<ApiClanWebhook>? ListClanWebhooks { get; set; }
    }

    public class MezonUpdateOnboardingStepByClanIdBody
    {
        [JsonPropertyName("onboarding_step")]
        public int? OnboardingStep { get; set; }
    }

    public class ApiListOnboardingStepResponse
    {
        [JsonPropertyName("list_onboarding_step")]
        public List<ApiOnboardingSteps>? ListOnboardingStep { get; set; }
    }

    public class ApiOnboardingSteps
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("onboarding_step")]
        public int? OnboardingStep { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class MezonapiCreateRoomChannelApps
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("room_name")]
        public string RoomName { get; set; }
    }

    public class ApiGenerateMeetTokenRequest
    {
        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("room_name")]
        public string RoomName { get; set; }
    }

    public class ApiGenerateMeetTokenResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    public class ApiCreateHashChannelAppsResponse
    {
        [JsonPropertyName("web_app_data")]
        public string WebAppData { get; set; }
    }

    public class ApiUserEventRequest
    {
        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("event_id")]
        public string EventId { get; set; }
    }

    public class ApiClanDiscover
    {
        [JsonPropertyName("about")]
        public string About { get; set; }

        [JsonPropertyName("banner")]
        public string Banner { get; set; }

        [JsonPropertyName("clan_id")]
        public string ClanId { get; set; }

        [JsonPropertyName("clan_logo")]
        public string ClanLogo { get; set; }

        [JsonPropertyName("clan_name")]
        public string ClanName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("invite_id")]
        public string InviteId { get; set; }

        [JsonPropertyName("online_members")]
        public int? OnlineMembers { get; set; }

        [JsonPropertyName("total_members")]
        public int? TotalMembers { get; set; }

        [JsonPropertyName("verified")]
        public bool? Verified { get; set; }
    }

    public class ApiListClanDiscover
    {
        [JsonPropertyName("clan_discover")]
        public List<ApiClanDiscover>? ClanDiscover { get; set; }

        [JsonPropertyName("page")]
        public int? Page { get; set; }

        [JsonPropertyName("page_count")]
        public int? PageCount { get; set; }
    }

    public class ApiClanDiscoverRequest
    {
        [JsonPropertyName("item_per_page")]
        public int? ItemPerPage { get; set; }

        [JsonPropertyName("page_number")]
        public int? PageNumber { get; set; }
    }

    #endregion
}
