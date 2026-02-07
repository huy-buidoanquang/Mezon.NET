using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Abstractions.Events;
using Mezon.NET.Api;
using Mezon.NET.DependencyInjection.Options;
using Mezon.NET.Socket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mezon.NET
{
    internal class MezonSocket : ISocket
    {
        private int _nextCid = 0;
        private int _heartbeatTimeoutInMilliseconds;
        private int _connectTimeoutInMilliseconds;
        private int _sendTimeoutInMilliseconds;
        private readonly bool _verbose;
        private readonly IWebSocketAdapter _adapter;
        private readonly ILogger<ISocket> _logger;
        private readonly IOptions<MezonApiClientOptions> _apiClientOptions;
        private readonly IOptions<MezonSocketOptions> _socketOptions;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _concurrentIds = new ConcurrentDictionary<string, TaskCompletionSource<object>>();
        private readonly CancellationTokenSource _pingPongCts = new CancellationTokenSource();

        protected MezonSocketOptions SocketOptions { get; private set; }
        protected Session Session { get; private set; }

        public bool IsOpen => _adapter.IsOpen();

        // --- Callbacks for connection state ---
        public event Action<object> OnDisconnect;
        public event Action<Exception> OnError;
        public event Action OnHeartbeatTimeout;
        public event Action<MezonSocketOpenEventArgs>? Connected;
        public event Action<SocketAdapterCloseEventArgs>? Disconnected;
        public event Action<SocketAdapterMessageEventArgs>? MessageReceived;
        public event Action<SocketAdapterErrorEventArgs>? ErrorOccurred;

        public event Action<NotificationEventArgs>? NotificationReceived;
        public event Action<MessageTypingEventArgs>? MessageTyping;

        internal MezonSocket(
            ILogger<ISocket> logger,
            IWebSocketAdapter adapter,
            IOptions<MezonApiClientOptions> mezonApiClientOptions,
            IOptions<MezonSocketOptions> mezonSocketOptions,
            bool verbose = false,
            int connectTimeoutInMilliseconds = -1,
            int sendTimeoutInMilliseconds = -1,
            int heartbeatTimeoutInMilliseconds = -1)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _apiClientOptions = mezonApiClientOptions ?? throw new ArgumentNullException(nameof(mezonApiClientOptions));
            _socketOptions = mezonSocketOptions ?? throw new ArgumentNullException(nameof(mezonSocketOptions));
            _verbose = verbose;
            _connectTimeoutInMilliseconds = connectTimeoutInMilliseconds;
            _sendTimeoutInMilliseconds = sendTimeoutInMilliseconds;
            _heartbeatTimeoutInMilliseconds = heartbeatTimeoutInMilliseconds;
        }

        public async Task<Session> ConnectAsync(Session session, bool createStatus, int? connectTimeoutInMilliseconds = null, CancellationToken cancellationToken = default)
        {
            ResolveOptions();
            Session = session;
            if (_adapter.IsOpen())
            {
                return session;
            }

            var sessionTaskSource = new TaskCompletionSource<Session>();

            _adapter.Closed += (sender, e) =>
            {
                if (_verbose)
                {
                    _logger.LogInformation($"Socket disconnected.");
                }

                Disconnected?.Invoke(e);
                _pingPongCts.Cancel();
            };

            _adapter.ErrorOccurred += (sender, e) =>
            {
                if (_verbose)
                {
                    _logger.LogInformation($"Socket error occurred.");
                }

                ErrorOccurred?.Invoke(e);
                sessionTaskSource.TrySetException(e.Exception);
            };

            _adapter.MessageReceived += WebSocketAdapter_MessageReceived;

            _adapter.Opened += (sender, e) =>
            {
                if (_verbose)
                {
                    _logger.LogInformation($"Socket connected.");
                }

                Connected?.Invoke((MezonSocketOpenEventArgs)e);
                _ = PingPongAsync(_pingPongCts.Token);
                sessionTaskSource.TrySetResult(session);
            };

            var timeout = connectTimeoutInMilliseconds ?? SocketOptions.ConnectTimeoutInMilliseconds;
            using (var cts = new CancellationTokenSource(timeout))
            {
                cts.Token.Register(() => sessionTaskSource.TrySetException(new TimeoutException("Socket connection timed out.")));
                await _adapter.ConnectAsync(SocketOptions.Scheme, SocketOptions.Host, SocketOptions.Port, createStatus, session.AuthToken, cancellationToken);
            }

            return await sessionTaskSource.Task;
        }

        private void WebSocketAdapter_MessageReceived(object sender, SocketAdapterMessageEventArgs e)
        {
            if (_verbose)
            {
                _logger.LogInformation("Socket message received.");
            }

            if (e.MessageNode?["cid"] is JsonNode cidNode && cidNode != null)
            {
                var cid = cidNode.GetValue<string>();
                if (_concurrentIds.TryRemove(cid, out var tcs))
                {
                    if (e.MessageNode?["error"] is JsonNode errorNode && errorNode != null)
                    {
                        tcs.TrySetException(new Exception(errorNode.ToJsonString()));
                    }
                    else
                    {
                        tcs.TrySetResult(e.MessageNode);
                    }
                }
                else
                {
                    if (_verbose)
                    {
                        _logger.LogWarning("No task completion source for message cid: {Cid}", cid);
                    }
                }
                return;
            }

            TryDispatchEvent(e.MessageNode, "message_typing_event", MessageTyping);
        }

        private bool TryDispatchEvent<T>(JsonNode? messageNode, string eventName, Action<T>? eventHandler)
            where T : MezonEventArgs
        {
            if (eventHandler != null
                && messageNode != null
                && messageNode[eventName] is JsonNode eventDataNode
                && eventDataNode != null)
            {
                try
                {
                    var eventData = eventDataNode.Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    eventHandler.Invoke(eventData);
                    return true;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize event: {EventName}", nameof(T));
                }
            }

            return false;
        }

        public Task CloseAsync(CancellationToken cancellation = default) => _adapter.CloseAsync(cancellation);

        /// <summary>
        /// Sends a message through the socket and waits for a corresponding response.
        /// </summary>
        /// <param name="message">The message object to send.</param>
        /// <param name="sendTimeout">The timeout in milliseconds to wait for a response.</param>
        /// <returns>A task that resolves with the response object or faults on timeout/error.</returns>
        public async Task<object> SendAsync<T>(
            T message,
            int sendTimeout = MezonSocketOptions.DefaultConnectTimeoutMs,
            CancellationToken cancellationToken = default)
            where T : SocketSendBase
        {
            if (!_adapter.IsOpen())
            {
                throw new InvalidOperationException("Socket connection has not been established yet.");
            }

            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            var jsonNode = JsonSerializer.SerializeToNode(message);
            if (jsonNode == null)
            {
                throw new ArgumentException("Failed to serialize the message.", nameof(message));
            }

            // Pre-process and stringify 'content' fields where necessary
            if (jsonNode["channel_message_send"]?["content"] is JsonNode sendContent)
            {
                jsonNode["channel_message_send"]!["content"] = JsonSerializer.Serialize(sendContent);
            }
            else if (jsonNode["channel_message_update"]?["content"] is JsonNode updateContent)
            {
                jsonNode["channel_message_update"]!["content"] = JsonSerializer.Serialize(updateContent);
            }
            else if (jsonNode["ephemeral_message_send"]?["message"]?["content"] is JsonNode ephemeralContent)
            {
                jsonNode["ephemeral_message_send"]!["message"]!["content"] = JsonSerializer.Serialize(ephemeralContent);
            }
            else if (jsonNode["quick_menu_event"]?["message"]?["content"] is JsonNode quickMenuContent)
            {
                jsonNode["quick_menu_event"]!["message"]!["content"] = JsonSerializer.Serialize(quickMenuContent);
            }

            var cid = GenerateCid();
            jsonNode["cid"] = cid;

            var timeoutCts = new CancellationTokenSource(sendTimeout);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                // Register a callback to handle timeout or external cancellation
                using var cancellationRegistration = linkedCts.Token.Register(() =>
                {
                    if (_concurrentIds.TryRemove(cid, out var entry))
                    {
                        if (timeoutCts.IsCancellationRequested)
                        {
                            entry.TrySetException(new TimeoutException("The socket timed out while waiting for a response."));
                        }
                        else
                        {
                            entry.TrySetException(new OperationCanceledException("The operation was canceled."));
                        }
                    }
                });

                if (!_concurrentIds.TryAdd(cid, tcs))
                {
                    throw new InvalidOperationException("Failed to send message due to a correlation ID collision.");
                }

                // Await the send operation and pass the linked cancellation token.
                await _adapter.SendAsync(jsonNode, linkedCts.Token).ConfigureAwait(false);
                _logger.LogInformation("Message sent with cid: {Cid}", cid);

                return await tcs.Task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (_concurrentIds.TryRemove(cid, out var entry))
                {
                    // The exception could be an OperationCanceledException if the token was triggered during the send.
                    entry.TrySetException(ex);
                }
                throw;
            }
            finally
            {
                // Ensure cleanup in case of any unexpected exit from the try block.
                timeoutCts.Dispose();
                linkedCts.Dispose();
                _concurrentIds.TryRemove(cid, out _);
            }
        }

        public Task DisconnectAsync(bool fireDisconnectEvent, CancellationToken cancellationToken = default)
        {
            if (_adapter.IsOpen())
            {
                _adapter.CloseAsync(cancellationToken);
            }

            if (fireDisconnectEvent)
            {
                Disconnected?.Invoke(new SocketAdapterCloseEventArgs(true, _nextCid, "", null));
            }

            return Task.CompletedTask;
        }

        public async Task<Channel> JoinChannelChatAsync(ChannelJoin channelJoin, CancellationToken cancellationToken = default)
        {
            var responseNode = await SendAsync(channelJoin, cancellationToken: cancellationToken) as JsonNode;
            return responseNode?["channel"]?.Deserialize<Channel>() ?? throw new InvalidOperationException("Invalid response from server for JoinChannelChat.");
        }

        public async Task JoinClanChatAsync(ClanJoin clanJoin, CancellationToken cancellationToken = default)
        {
            var responseNode = await SendAsync(clanJoin, cancellationToken: cancellationToken) as JsonNode;
            _logger.LogInformation("Heartbeat pong received: {Pong}", responseNode.ToJsonString());
        }

        public Task LeaveChannelChatAsync(string clanId, string channelId, int channelType, bool isPublic, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Socket.ChannelMessageAck> RemoveChatMessageAsync(
            string clanId,
            string channelId,
            int mode,
            bool isPublic,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        //public Task<TokenSentEvent> SendTokenAsync(string receiverId, decimal amount, CancellationToken cancellationToken = default)
        //{
        //    throw new NotImplementedException();
        //}

        public Task<Socket.ChannelMessageAck> UpdateChatMessageAsync(
            string clanId,
            string channelId,
            int mode,
            bool isPublic,
            string messageId,
            object content,
            IEnumerable<ApiMessageMention>? mentions = null,
            IEnumerable<ApiMessageAttachment>? attachments = null,
            bool? hideEdited = null,
            string? topicId = null,
            bool? isUpdateMsgTopic = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UpdateStatusAsync(string? status = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<Socket.ChannelMessageAck> WriteChatMessageAsync(
            string clanId,
            string channelId,
            int mode,
            bool isPublic,
            object? content = null,
            IEnumerable<ApiMessageMention>? mentions = null,
            IEnumerable<ApiMessageAttachment>? attachments = null,
            IEnumerable<ApiMessageRef>? references = null,
            bool? anonymousMessage = null,
            bool? mentionEveryone = null,
            string? avatar = null,
            int? code = null,
            string? topicId = null,
            CancellationToken cancellationToken = default)
        {
            //var payload = new
            //{
            //    channel_message_send = new
            //    {
            //        clan_id = clanId,
            //        channel_id = channelId,
            //        mode,
            //        is_public = isPublic,
            //        content,
            //        mentions,
            //        attachments,
            //        references,
            //        anonymous_message = anonymousMessage,
            //        mention_everyone = mentionEveryone,
            //        avatar,
            //        code,
            //        topic_id = topicId
            //    }
            //};

            //await SendAsync(payload, cancellationToken: cancellationToken);
            throw new NotImplementedException();
        }

        public Task<ApiMessageReaction> WriteMessageReactionAsync(
            string id,
            string clanId,
            string channelId,
            int mode,
            bool isPublic,
            string messageId,
            string emojiId,
            string emoji,
            int count,
            string messageSenderId,
            bool actionDelete,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        //public Task<MessageTypingEvent> WriteMessageTypingAsync(
        //    string clanId,
        //    string channelId,
        //    int mode,
        //    bool isPublic,
        //    CancellationToken cancellationToken = default)
        //{
        //    throw new NotImplementedException();
        //}

        #region private methods
        private string GenerateCid() => Interlocked.Increment(ref _nextCid).ToString();

        private async Task PingPongAsync(CancellationToken cancellationToken = default)
        {
            if (!_adapter.IsOpen())
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await SendAsync(new MessagePing(), SocketOptions.HeartbeatTimeoutInMilliseconds, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Heartbeat ping sent.");
            }
            catch (Exception ex)
            {
                if (_adapter.IsOpen())
                {
                    if (_verbose)
                    {
                        _logger.LogInformation("Server unreachable from heartbeat.");
                    }

                    OnHeartbeatTimeout?.Invoke();
                    await _adapter.CloseAsync(cancellationToken);
                }

                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(SocketOptions.HeartbeatTimeoutInMilliseconds));
            await PingPongAsync(cancellationToken);
        }

        private void ResolveOptions()
        {
            if (SocketOptions != null)
            {
                return;
            }

            var apiClientOptions = _apiClientOptions.Value;
            if (string.IsNullOrEmpty(apiClientOptions.ApiBasePath))
            {
                throw new InvalidOperationException("MezonApiClientOptions.GatewayBasePath is not configured. Ensure API client is configured first.");
            }

            var socketOptions = _socketOptions.Value;
            var uri = new Uri(apiClientOptions.ApiBasePath);
            SocketOptions = new MezonSocketOptions
            {
                Host = uri.Host,
                Port = uri.Port,
                UseSSL = apiClientOptions.UseSSL,
                Scheme = apiClientOptions.UseSSL ? "wss" : "ws",

                HeartbeatTimeoutInMilliseconds = _heartbeatTimeoutInMilliseconds > 0 ? _heartbeatTimeoutInMilliseconds : socketOptions.HeartbeatTimeoutInMilliseconds,
                ConnectTimeoutInMilliseconds = _connectTimeoutInMilliseconds > 0 ? _connectTimeoutInMilliseconds : socketOptions.ConnectTimeoutInMilliseconds,
                SendTimeoutInMilliseconds = _sendTimeoutInMilliseconds > 0 ? _sendTimeoutInMilliseconds : socketOptions.SendTimeoutInMilliseconds
            };
        }
        #endregion

    }
}
