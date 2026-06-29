// Copyright 2024 The Mezon Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Abstractions.Managers;
using Mezon.NET.Api;
using Mezon.NET.Api.ApiRequests;
using Mezon.NET.Api.ApiResponses;
using Mezon.NET.DependencyInjection.Options;
using Mezon.NET.Exceptions;
using Mezon.NET.Managers;
using Mezon.NET.Socket;
using Microsoft.Extensions.Options;

namespace Mezon.NET
{
    #region Enums

    public enum ChannelType
    {
        Channel = 1,
        Group = 2,
        DM = 3,
        GmeetVoice = 4,
        Forum = 5,
        Streaming = 6,
        Thread = 7,
        App = 8,
        Announcement = 9,
        MezonVoice = 10
    }

    public enum ChannelStreamMode
    {
        Channel = 2,
        Group = 3,
        DM = 4,
        Clan = 5,
        Thread = 6,
    }

    public enum NotificationType
    {
        AllMessage = 1,
        MentionMessage = 2,
        NothingMessage = 3,
    }

    public enum WebrtcSignalingType
    {
        SdpInit = 0,
        SdpOffer = 1,
        SdpAnswer = 2,
        IceCandidate = 3,
        SdpQuit = 4,
        SdpTimeout = 5,
        SdpNotAvailable = 6,
        SdpJoinedOtherCall = 7,
        SdpStatusRemoteMedia = 8
    }

    #endregion

    #region Public Interfaces & Models

    /// <summary>
    /// Response for an RPC function executed on the server.
    /// </summary>
    public interface IRpcResponse
    {
        /// <summary>The identifier of the function.</summary>
        string? Id { get; }

        /// <summary>The payload of the function which must be a JSON object.</summary>
        JsonElement? Payload { get; }
    }

    /// <inheritdoc />
    public class RpcResponse : IRpcResponse
    {
        public string Id { get; set; }
        public JsonElement? Payload { get; set; }
    }

    /// <inheritdoc />
    public class ChannelMessage
    {
        public string Id { get; set; }
        public string Avatar { get; set; }
        public string ChannelId { get; set; }
        public string ChannelLabel { get; set; }
        public string ClanId { get; set; }
        public int Code { get; set; }
        public JsonElement? Content { get; set; }
        public string CreateTime { get; set; }
        public IEnumerable<ApiMessageReaction>? Reactions { get; set; }
        public IEnumerable<ApiMessageMention>? Mentions { get; set; }
        public IEnumerable<ApiMessageAttachment>? Attachments { get; set; }
        public IEnumerable<ApiMessageRef>? References { get; set; }
        public IEnumerable<string>? ReferencedMessage { get; set; }
        public bool? Persistent { get; set; }
        public string SenderId { get; set; }
        public string UpdateTime { get; set; }
        public string ClanLogo { get; set; }
        public string CategoryName { get; set; }
        public string Username { get; set; }
        public string ClanNick { get; set; }
        public string ClanAvatar { get; set; }
        public string DisplayName { get; set; }
        public long? CreateTimeSeconds { get; set; }
        public long? UpdateTimeSeconds { get; set; }
        public int? Mode { get; set; }
        public string MessageId { get; set; }
        public bool? HideEditted { get; set; }
        public bool? IsPublic { get; set; }
        public string TopicId { get; set; }
    }

    /// <inheritdoc />
    public class ChannelMessageList
    {
        public string CacheableCursor { get; set; }
        public ApiChannelMessageHeader? LastSeenMessage { get; set; }
        public ApiChannelMessageHeader? LastSentMessage { get; set; }
        public IEnumerable<ChannelMessage>? Messages { get; set; }
        public string NextCursor { get; set; }
        public string PrevCursor { get; set; }
    }
    /// <inheritdoc />
    public class Friend
    {
        public int? State { get; set; }
        public ApiUser User { get; set; }
    }

    /// <inheritdoc />
    public class Friends
    {
        public IEnumerable<Friend> FriendList { get; set; }
        public string Cursor { get; set; }
    }

    #endregion

    /// <summary>
    /// A client for Mezon server.
    /// </summary>
    public class MezonClient
    {
        public IMezonApiClient MezonApiClient { get; private set; }
        protected MezonClientOptions Options { get; private set; }
        public ISessionManager SessionManager { get; private set; }
        public ISocketManager SocketManager { get; private set; }
        protected string Token { get; private set; }
        protected string ClientId { get; private set; }
        protected string Host { get; private set; }
        protected int Port { get; private set; }
        protected bool UseSSL { get; private set; }
        protected bool AutoRefreshSession { get; set; }
        protected string LoginBasePath { get; private set; }

        public MezonClient(
            IMezonApiClient mezonApiClient,
            ISessionManager sessionManager,
            ISocketManager socketManager,
            IOptions<MezonClientOptions> options)
        {
            MezonApiClient = mezonApiClient;
            SessionManager = sessionManager;
            SocketManager = socketManager;
            Options = options.Value ?? throw new ArgumentNullException(nameof(options));

            Token = Options.AppToken;
            Host = Options.Host;
            Port = Options.Port;
            UseSSL = Options.UseSSL;
            AutoRefreshSession = Options.AutoRefreshSession;
        }

        #region Public Methods

        /// <summary>
        /// Logs the bot in and establishes a session.
        /// </summary>
        /// <returns>A string containing the JSON representation of the session.</returns>
        public async Task<string> LoginAsync(CancellationToken cancellationToken = default)
        {
            if (!await SessionManager.AuthenticateAsync(Token, AutoRefreshSession))
            {
                throw new MezonApiUnauthorizedException("Authentication failed, API session is null.");
            }

            var session = SessionManager.CurrentSession();
            if (!string.IsNullOrEmpty(session.ApiUrl))
            {
                ConfigureApiBasePath(session.ApiUrl);
                //InitManager(basePath, session);
            }

            ClientId = session.UserId;

            //var sessionConnected = await _socketManager.ConnectAsync(session);
            //if (!string.IsNullOrEmpty(sessionConnected?.Token))
            //{
            //    await _socketManager.ConnectSocketAsync(sessionConnected.Token);
            //    await _channelManager.InitAllDmChannelsAsync(sessionConnected.Token);
            //}

            //this.Emit("ready"); // Assuming an event emitter pattern exists

            // Use System.Text.Json.JsonSerializer to convert the object to a string
            SocketManager.CreateSocket();
            await SocketManager.ConnectSocketAsync(cancellationToken);
            return JsonSerializer.Serialize(session);
        }

        /// <summary>
        /// Check if a session has expired.
        /// </summary>
        /// <param name="session">The session to check.</param>
        /// <returns>True if the session is expired.</returns>
        public bool IsExpired(ISession session)
        {
            return session.IsExpired(DateTime.UtcNow);
        }

        /// <summary>
        /// Set a new base path for the API client.
        /// </summary>
        public void ConfigureApiBasePath(string apiUrl)
        {
            var uri = new Uri(apiUrl);
            Host = uri.Host;
            Port = uri.IsDefaultPort ? (UseSSL ? 443 : 80) : uri.Port;
            UseSSL = uri.Scheme == "https";
            var scheme = UseSSL ? "https" : "http";
            var apiBasePath = $"{scheme}://{Host}:{Port}";
            MezonApiClient.ConfigureMezonApiBasePath(apiBasePath);
        }

        /// <summary>
        /// Creates a new socket connection.
        /// </summary>
        //public ISocket CreateSocket(bool verbose = false)
        //{
        //    // Note: IWebSocketAdapter can be injected for different environments (e.g., text vs binary protocol)
        //    var adapter = new WebSocketAdapterText();
        //    return new MezonSocket(adapter, null, false, DefaultTimeoutMs);
        //}

        /// <summary>
        /// Authenticate a user with a Mezon token.
        /// </summary>
        //public async Task<ISession> AuthenticateMezonAsync(string token, bool? create = null, string? username = null, bool? isRemember = null, IDictionary<string, string>? vars = null)
        //{
        //    var request = new ApiAccountMezon { Token = token, Vars = vars };
        //    var apiSession = await _apiClient.AuthenticateMezonAsync("defaultKey", "", request, create, username, isRemember);
        //    return new Session(apiSession.Token, apiSession.RefreshToken, (bool)apiSession.Created);
        //}

        /// <summary>
        /// Authenticate a user with an email and password.
        /// </summary>
        //public async Task<ISession> AuthenticateEmailAsync(string email, string password, string? username = null, bool create = true, IDictionary<string, string>? vars = null)
        //{
        //    var request = new ApiAuthenticateEmailRequest
        //    {
        //        Account = new ApiAccountEmail { Email = email, Password = password, Vars = vars },
        //        Create = create,
        //        Username = username,
        //    };
        //    var apiSession = await _apiClient.AuthenticateEmailAsync("", "", request);
        //    return new Session(apiSession.Token, apiSession.RefreshToken, (bool)apiSession.Created);
        //}

        /// <summary>
        /// Log out a session, invalidate a refresh token, or log out all sessions/refresh tokens for a user.
        /// </summary>
        public async Task LogoutAsync(ISession session, string? deviceId = null, string? platform = null)
        {
            var request = new ApiSessionLogoutRequest
            {
                Token = session.AuthToken,
                RefreshToken = session.RefreshToken,
                DeviceId = deviceId,
                Platform = platform,
            };
            //await _apiClient.SessionLogoutAsync(request);
        }

        /// <summary>
        /// Fetches the current user's account details.
        /// </summary>
        public async Task<AccountResponse> GetAccountAsync(ISession session)
        {
            return await MezonApiClient.GetAccountAsync(session.AuthToken);
        }

        /// <summary>
        /// Updates fields in the current user's account.
        /// </summary>
        public async Task UpdateAccountAsync(ISession session, UpdateAccountRequest request)
        {
            await MezonApiClient.UpdateAccountAsync(session.AuthToken, request);
        }

        /// <summary>
        /// Deletes the current user's account.
        /// </summary>
        public async Task DeleteAccountAsync(ISession session)
        {
            await MezonApiClient.DeleteAccountAsync(session.AuthToken);
        }

        /// <summary>
        /// List all friends for the current user.
        /// </summary>
        //public async Task<Friends> ListFriendsAsync(ISession session, int? state = null, int? limit = null, string? cursor = null)
        //{
        //    await RefreshSessionAsync(session);
        //    var apiFriendList = await _apiClient.ListFriendsAsync(session.AuthToken, state, limit, cursor);

        //    var friends = new Friends
        //    {
        //        Cursor = apiFriendList.Cursor,
        //        FriendList = apiFriendList.Friends?.Select(f => new Friend
        //        {
        //            State = f.State,
        //            User = f.User
        //        }).ToList() ?? new List<Friend>()
        //    };

        //    return friends;
        //}

        /// <summary>
        /// List a channel's message history.
        /// </summary>
        public async Task<ChannelMessageList> ListChannelMessagesAsync(ISession session, string channelId, string? clanId = null, int limit = 20, string? cursor = null)
        {
            // Assuming ListChannelMessages is implemented in MezonApi
            //var response = await _apiClient.ListChannelMessagesAsync(session.AuthToken, clanId, channelId, limit, cursor);
            //return MapToChannelMessageList(response);
            throw new NotImplementedException("This method requires the full MezonApi implementation.");
        }

        public async Task<ClanDescriptionsResponse> ClanDescriptionsAsync(CancellationToken cancellationToken = default)
        {
            // Assuming ListChannelMessages is implemented in MezonApi
            var response = await MezonApiClient.GetClanDescriptionsAsync(SessionManager.CurrentSession().AuthToken, cancellationToken: cancellationToken);
            return response;
            //throw new NotImplementedException("This method requires the full MezonApi implementation.");
        }

        // ... (ALL other ~150+ methods from client.ts would be implemented here following the same pattern) ...
        // Each method would call RefreshSessionAsync, then call the appropriate _apiClient method,
        // and finally map the result to the public-facing interface models if necessary.

        #endregion

        #region Private Methods

        // Example mapper function
        private ChannelMessageList MapToChannelMessageList(ApiChannelMessageList apiList)
        {
            return new ChannelMessageList
            {
                //CacheableCursor = apiList.CacheableCursor,
                LastSeenMessage = apiList.LastSeenMessage,
                LastSentMessage = apiList.LastSentMessage,
                // PrevCursor and NextCursor need to be added to ApiChannelMessageList if they exist in the response
                Messages = apiList.Messages?.Select(m => new ChannelMessage
                {
                    Id = m.MessageId,
                    ChannelId = m.ChannelId,
                    Content = string.IsNullOrEmpty(m.Content) ? (JsonElement?)null : JsonDocument.Parse(m.Content).RootElement,
                    //... map all other properties
                }).ToList()
            };
        }

        #endregion
    }
}
