using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mezon.NET.SDK.Abstractions
{
    /// <summary>
    /// Interface for Mezon Client SDK
    /// Provides methods for authentication, messaging, token transfers, and event handling
    /// </summary>
    public interface IMezonClient
    {
        #region Properties

        /// <summary>
        /// Authentication token
        /// </summary>
        string Token { get; }

        /// <summary>
        /// Bot/Client identifier
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// Server host address
        /// </summary>
        string Host { get; }

        /// <summary>
        /// Whether to use SSL/TLS
        /// </summary>
        bool UseSSL { get; }

        /// <summary>
        /// Server port
        /// </summary>
        string Port { get; }

        /// <summary>
        /// Login base path URL
        /// </summary>
        string LoginBasePath { get; }

        /// <summary>
        /// MMN API URL
        /// </summary>
        string MmnApiUrl { get; }

        /// <summary>
        /// ZK API URL
        /// </summary>
        string ZkApiUrl { get; }

        /// <summary>
        /// MMN address
        /// </summary>
        string AddressMMN { get; }

        /// <summary>
        /// Cache manager for clans
        /// </summary>
        ICacheManager<string, IClan> Clans { get; }

        /// <summary>
        /// Cache manager for channels
        /// </summary>
        ICacheManager<string, ITextChannel> Channels { get; }

        #endregion

        #region Authentication & Lifecycle

        /// <summary>
        /// Initialize the client managers
        /// </summary>
        /// <param name="basePath">Base API path</param>
        /// <param name="sessionApi">Optional session object</param>
        void InitManager(string basePath, object sessionApi = null);

        /// <summary>
        /// Login to the Mezon service
        /// </summary>
        /// <returns>JSON string of session information</returns>
        Task<string> LoginAsync();

        /// <summary>
        /// Close the socket connection
        /// </summary>
        void CloseSocket();

        #endregion

        #region Channel Operations

        /// <summary>
        /// Create a direct message channel with a user
        /// </summary>
        /// <param name="userId">Target user ID</param>
        /// <returns>Channel descriptor or null</returns>
        Task<object> CreateDMChannelAsync(string userId);

        #endregion

        #region Token & Crypto Operations

        /// <summary>
        /// Get ephemeral key pair for cryptographic operations
        /// </summary>
        /// <returns>Ephemeral key pair</returns>
        Task<object> GetEphemeralKeyPairAsync();

        /// <summary>
        /// Get blockchain address from user ID
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Blockchain address</returns>
        Task<string> GetAddressAsync(string userId);

        /// <summary>
        /// Get zero-knowledge proofs
        /// </summary>
        /// <param name="request">ZK proof request data</param>
        /// <returns>ZK proof object</returns>
        Task<object> GetZkProofsAsync(ApiGetZkProofRequest request);

        /// <summary>
        /// Get current nonce for transaction
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="tag">Nonce tag: "latest" or "pending"</param>
        /// <returns>Current nonce</returns>
        Task<object> GetCurrentNonceAsync(string userId, string tag = "pending");

        /// <summary>
        /// Send tokens to another user
        /// </summary>
        /// <param name="tokenEvent">Token transfer request</param>
        /// <returns>Transaction result</returns>
        Task<object> SendTokenAsync(ApiSentTokenRequest tokenEvent);

        #endregion

        #region Quick Menu Operations

        /// <summary>
        /// Add quick menu access
        /// </summary>
        /// <param name="payload">Quick menu access payload</param>
        /// <returns>Result of the operation</returns>
        Task<object> AddQuickMenuAccessAsync(ApiQuickMenuAccessPayload payload);

        /// <summary>
        /// Delete quick menu access
        /// </summary>
        /// <param name="botId">Optional bot ID, defaults to current client ID</param>
        /// <returns>Result of the operation</returns>
        Task<object> DeleteQuickMenuAccessAsync(string botId = null);

        #endregion

        #region Friend Operations

        /// <summary>
        /// Get list of friends
        /// </summary>
        /// <param name="limit">Maximum number of results</param>
        /// <param name="state">Friend state filter</param>
        /// <param name="cursor">Pagination cursor</param>
        /// <returns>List of friends</returns>
        Task<object> GetListFriendsAsync(int? limit = null, string state = null, string cursor = null);

        /// <summary>
        /// Accept friend request
        /// </summary>
        /// <param name="userId">User ID to accept</param>
        /// <param name="username">Username</param>
        /// <returns>Result of the operation</returns>
        Task<object> AcceptFriendAsync(string userId, string username);

        /// <summary>
        /// Send friend request
        /// </summary>
        /// <param name="username">Username to add</param>
        /// <returns>Result of the operation</returns>
        Task<object> AddFriendAsync(string username);

        #endregion

        #region Event Handlers

        /// <summary>
        /// Register handler for channel message events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnChannelMessage(Action<ChannelMessage> handler);

        /// <summary>
        /// Register handler for channel created events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnChannelCreated(Action<ChannelCreatedEvent> handler);

        /// <summary>
        /// Register handler for channel updated events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnChannelUpdated(Action<ChannelUpdatedEvent> handler);

        /// <summary>
        /// Register handler for channel deleted events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnChannelDeleted(Action<ChannelDeletedEvent> handler);

        /// <summary>
        /// Register handler for token send events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnTokenSend(Action<TokenSentEvent> handler);

        /// <summary>
        /// Register handler for message reaction events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnMessageReaction(Action<object> handler);

        /// <summary>
        /// Register handler for user channel removed events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnUserChannelRemoved(Action<UserChannelRemoved> handler);

        /// <summary>
        /// Register handler for user clan removed events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnUserClanRemoved(Action<UserClanRemovedEvent> handler);

        /// <summary>
        /// Register handler for user channel added events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnUserChannelAdded(Action<UserChannelAddedEvent> handler);

        /// <summary>
        /// Register handler for give coffee events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnGiveCoffee(Action<GiveCoffeeEvent> handler);

        /// <summary>
        /// Register handler for role events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnRoleEvent(Action<RoleEvent> handler);

        /// <summary>
        /// Register handler for role assign events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnRoleAssign(Action<RoleAssignedEvent> handler);

        /// <summary>
        /// Register handler for notification events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnNotification(Action<Notifications> handler);

        /// <summary>
        /// Register handler for add clan user events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnAddClanUser(Action<AddClanUserEvent> handler);

        /// <summary>
        /// Register handler for clan event created events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnClanEventCreated(Action<CreateEventRequest> handler);

        /// <summary>
        /// Register handler for message button clicked events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnMessageButtonClicked(Action<MessageButtonClicked> handler);

        /// <summary>
        /// Register handler for streaming joined events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnStreamingJoinedEvent(Action<StreamingJoinedEvent> handler);

        /// <summary>
        /// Register handler for streaming leaved events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnStreamingLeavedEvent(Action<StreamingLeavedEvent> handler);

        /// <summary>
        /// Register handler for dropdown box selected events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnDropdownBoxSelected(Action<DropdownBoxSelected> handler);

        /// <summary>
        /// Register handler for WebRTC signaling forward events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnWebrtcSignalingFwd(Action<WebrtcSignalingFwd> handler);

        /// <summary>
        /// Register handler for voice started events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnVoiceStartedEvent(Action<VoiceStartedEvent> handler);

        /// <summary>
        /// Register handler for voice ended events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnVoiceEndedEvent(Action<VoiceEndedEvent> handler);

        /// <summary>
        /// Register handler for voice joined events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnVoiceJoinedEvent(Action<VoiceJoinedEvent> handler);

        /// <summary>
        /// Register handler for voice leaved events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnVoiceLeavedEvent(Action<VoiceLeavedEvent> handler);

        /// <summary>
        /// Register handler for quick menu events
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <returns>Client instance for chaining</returns>
        IMezonClient OnQuickMenuEvent(Action<object> handler);

        #endregion

        #region Events

        /// <summary>
        /// Event raised when client is ready
        /// </summary>
        event EventHandler Ready;

        #endregion
    }

    #region Supporting Interfaces

    public interface IClan
    {
        string Id { get; }
        string Name { get; }
        ICacheManager<string, ITextChannel> Channels { get; }
        ICacheManager<string, IUser> Users { get; }
        Task LoadChannelsAsync();
    }

    public interface ITextChannel
    {
        string Id { get; }
        string ClanId { get; }
        ICacheManager<string, IMessage> Messages { get; }
    }

    public interface IUser
    {
        string Id { get; }
        string Username { get; }
        string DisplayName { get; }
        Task SendDMAsync(object content, int messageType);
    }

    public interface IMessage
    {
        string Id { get; }
        string ChannelId { get; }
        string SenderId { get; }
        object Content { get; }
    }

    #endregion

    #region Event Data Classes

    public class ChannelMessage
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public string SenderId { get; set; }
        public string MessageId { get; set; }
        public object Content { get; set; }
        public string Username { get; set; }
        public string ClanNick { get; set; }
        public string ClanAvatar { get; set; }
        public string Avatar { get; set; }
        public string DisplayName { get; set; }
        public object Reactions { get; set; }
        public object Mentions { get; set; }
        public object Attachments { get; set; }
        public object References { get; set; }
        public long CreateTimeSeconds { get; set; }
        public string TopicId { get; set; }
    }

    public class ChannelCreatedEvent
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public int ChannelType { get; set; }
        public string ChannelLabel { get; set; }
        public int ChannelPrivate { get; set; }
    }

    public class ChannelUpdatedEvent
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public int ChannelType { get; set; }
        public string ChannelLabel { get; set; }
        public int Status { get; set; }
        public int ChannelPrivate { get; set; }
    }

    public class ChannelDeletedEvent
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
    }

    public class TokenSentEvent
    {
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public string Amount { get; set; }
        public string Note { get; set; }
    }

    public class UserChannelRemoved
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public List<string> UserIds { get; set; }
    }

    public class UserClanRemovedEvent
    {
        public string ClanId { get; set; }
        public List<string> UserIds { get; set; }
    }

    public class UserChannelAddedEvent
    {
        public string ClanId { get; set; }
        public object ChannelDesc { get; set; }
        public List<object> Users { get; set; }
    }

    public class GiveCoffeeEvent
    {
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public int Amount { get; set; }
    }

    public class RoleEvent
    {
        public string RoleId { get; set; }
        public string ClanId { get; set; }
        public string Action { get; set; }
    }

    public class RoleAssignedEvent
    {
        public string RoleId { get; set; }
        public string UserId { get; set; }
        public string ClanId { get; set; }
    }

    public class Notifications
    {
        public List<Notification> NotificationList { get; set; }
    }

    public class Notification
    {
        public string SenderId { get; set; }
        public int Code { get; set; }
        public string Content { get; set; }
    }

    public class AddClanUserEvent
    {
        public string ClanId { get; set; }
        public UserInfo User { get; set; }
    }

    public class UserInfo
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Avatar { get; set; }
        public string DisplayName { get; set; }
    }

    public class CreateEventRequest
    {
        public string ClanId { get; set; }
        public string EventName { get; set; }
        public string Description { get; set; }
        public long StartTime { get; set; }
        public long EndTime { get; set; }
    }

    public class MessageButtonClicked
    {
        public string MessageId { get; set; }
        public string ButtonId { get; set; }
        public string UserId { get; set; }
    }

    public class StreamingJoinedEvent
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public string UserId { get; set; }
    }

    public class StreamingLeavedEvent
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public string UserId { get; set; }
    }

    public class DropdownBoxSelected
    {
        public string MessageId { get; set; }
        public string SelectedValue { get; set; }
        public string UserId { get; set; }
    }

    public class WebrtcSignalingFwd
    {
        public string Data { get; set; }
    }

    public class VoiceStartedEvent
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public string UserId { get; set; }
    }

    public class VoiceEndedEvent
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public string UserId { get; set; }
    }

    public class VoiceJoinedEvent
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public string UserId { get; set; }
    }

    public class VoiceLeavedEvent
    {
        public string ClanId { get; set; }
        public string ChannelId { get; set; }
        public string UserId { get; set; }
    }

    #endregion

    #region Request Data Classes

    public class ApiGetZkProofRequest
    {
        public string UserId { get; set; }
        public string Jwt { get; set; }
        public string Address { get; set; }
        public string EphemeralPublicKey { get; set; }
    }

    public class ApiSentTokenRequest
    {
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; }
        public string SenderName { get; set; }
        public string ExtraAttribute { get; set; }
        public object MmnExtraInfo { get; set; }
    }

    public class ApiQuickMenuAccessPayload
    {
        public string ClanId { get; set; }
        public int MenuType { get; set; }
        public string ActionMsg { get; set; }
        public string Background { get; set; }
        public string MenuName { get; set; }
    }

    #endregion
}

