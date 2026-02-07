using System.Text.Json.Serialization;
using Mezon.NET.Api;
using Mezon.NET.Api.ApiResponses;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a flexible data payload for various socket operations.
    /// Only one of these properties is expected to be populated per message.
    /// </summary>
    public class ListDataSocket : SocketSendBase
    {
        [JsonPropertyName("api_name")]
        public string ApiName { get; set; }

        [JsonPropertyName("list_clan_req")]
        public object? ListClanReq { get; set; }

        [JsonPropertyName("clan_desc_list")]
        public ApiClanDescList? ClanDescList { get; set; }

        [JsonPropertyName("list_thread_req")]
        public object? ListThreadReq { get; set; }

        [JsonPropertyName("channel_desc_list")]
        public ApiChannelDescList? ChannelDescList { get; set; }

        [JsonPropertyName("list_channel_users_uc_req")]
        public object? ListChannelUsersUcReq { get; set; }

        [JsonPropertyName("channel_users_uc_list")]
        public ApiAllUsersAddChannelResponse? ChannelUsersUcList { get; set; }

        [JsonPropertyName("list_channel_detail_req")]
        public object? ListChannelDetailReq { get; set; }

        [JsonPropertyName("channel_desc")]
        public ApiChannelDescription? ChannelDesc { get; set; }

        [JsonPropertyName("list_channel_req")]
        public object? ListChannelReq { get; set; }

        [JsonPropertyName("list_channel_message_req")]
        public object? ListChannelMessageReq { get; set; }

        [JsonPropertyName("channel_message_list")]
        public ApiChannelMessageList? ChannelMessageList { get; set; }

        [JsonPropertyName("list_channel_users_req")]
        public object? ListChannelUsersReq { get; set; }

        [JsonPropertyName("voice_user_list")]
        public ApiVoiceChannelUserList? VoiceUserList { get; set; }

        [JsonPropertyName("channel_user_list")]
        public ApiChannelUserList? ChannelUserList { get; set; }

        [JsonPropertyName("list_channel_attachment_req")]
        public object? ListChannelAttachmentReq { get; set; }

        [JsonPropertyName("channel_attachment_list")]
        public ApiChannelAttachmentList? ChannelAttachmentList { get; set; }

        [JsonPropertyName("hashtag_dm_req")]
        public object? HashtagDmReq { get; set; }

        [JsonPropertyName("hashtag_dm_list")]
        public ApiHashtagDmList? HashtagDmList { get; set; }

        [JsonPropertyName("channel_setting_req")]
        public object? ChannelSettingReq { get; set; }

        [JsonPropertyName("channel_setting_list")]
        public ApiChannelSettingListResponse? ChannelSettingList { get; set; }

        [JsonPropertyName("favorite_channel_req")]
        public object? FavoriteChannelReq { get; set; }

        [JsonPropertyName("favorite_channel_list")]
        public ApiListFavoriteChannelResponse? FavoriteChannelList { get; set; }

        [JsonPropertyName("search_thread_req")]
        public object? SearchThreadReq { get; set; }

        [JsonPropertyName("notification_channel")]
        public ApiNotificationChannel? NotificationChannel { get; set; }

        // Note: C# property 'NotificationUserChannel' corrects a typo in the original 'notificaion_user_channel'.
        [JsonPropertyName("notificaion_user_channel")]
        public ApiNotificationUserChannel? NotificationUserChannel { get; set; }

        [JsonPropertyName("notification_category")]
        public object? NotificationCategory { get; set; }

        [JsonPropertyName("notification_clan")]
        public object? NotificationClan { get; set; }

        [JsonPropertyName("notification_setting")]
        public ApiNotificationSetting? NotificationSetting { get; set; }

        [JsonPropertyName("notification_message")]
        public ApiNotifiReactMessage? NotificationMessage { get; set; }

        [JsonPropertyName("noti_channel_cat_setting_list")]
        public ApiNotificationChannelCategorySettingList? NotiChannelCatSettingList { get; set; }

        [JsonPropertyName("list_notification_req")]
        public object? ListNotificationReq { get; set; }

        [JsonPropertyName("notification_list")]
        public ApiNotificationList? NotificationList { get; set; }

        [JsonPropertyName("sticker_list")]
        public ApiStickerListedResponse? StickerList { get; set; }

        //[JsonPropertyName("emoji_recent_list")]
        //public ApiEmojiRecentList? EmojiRecentList { get; set; }

        [JsonPropertyName("clan_webhook_req")]
        public object? ClanWebhookReq { get; set; }

        [JsonPropertyName("clan_webhook_list")]
        public ApiListClanWebhookResponse? ClanWebhookList { get; set; }

        [JsonPropertyName("webhook_list_req")]
        public object? WebhookListReq { get; set; }

        [JsonPropertyName("webhook_list")]
        public ApiWebhookListResponse? WebhookList { get; set; }

        [JsonPropertyName("permission_list_req")]
        public object? PermissionListReq { get; set; }

        [JsonPropertyName("permission_list")]
        public ApiPermissionList? PermissionList { get; set; }

        [JsonPropertyName("role_user_req")]
        public object? RoleUserReq { get; set; }

        [JsonPropertyName("role_user_list")]
        public ApiRoleUserList? RoleUserList { get; set; }

        [JsonPropertyName("permission_user_req")]
        public object? PermissionUserReq { get; set; }

        [JsonPropertyName("role_list")]
        public RolesResponse? RoleList { get; set; }

        [JsonPropertyName("role_list_event_req")]
        public object? RoleListEventReq { get; set; }

        [JsonPropertyName("role_event_list")]
        public RoleEventResponse? RoleEventList { get; set; }

        [JsonPropertyName("user_permission_req")]
        public object? UserPermissionReq { get; set; }

        [JsonPropertyName("user_permission_list")]
        public ApiUserPermissionInChannelListResponse? UserPermissionList { get; set; }

        [JsonPropertyName("permission_role_req")]
        public object? PermissionRoleReq { get; set; }

        [JsonPropertyName("permission_role_list")]
        public ApiPermissionRoleChannelListEventResponse? PermissionRoleList { get; set; }

        [JsonPropertyName("emoji_list")]
        public ApiEmojiListedResponse? EmojiList { get; set; }

        [JsonPropertyName("list_friend_req")]
        public object? ListFriendReq { get; set; }

        [JsonPropertyName("friend_list")]
        public ApiFriendList? FriendList { get; set; }

        [JsonPropertyName("list_apps_req")]
        public object? ListAppsReq { get; set; }

        [JsonPropertyName("channel_apps_list")]
        public ApiListChannelAppsResponse? ChannelAppsList { get; set; }

        [JsonPropertyName("user_activity_list")]
        public ApiListUserActivity? UserActivityList { get; set; }
    }
}
