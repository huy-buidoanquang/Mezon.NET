//// Socket/DefaultSocket.cs

//using Mezon.NET.Abstractions;
//using Mezon.NET.Api;
//using Mezon.NET.Utils;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Text.Json.Serialization;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Web;

//namespace Mezon.NET.Socket
//{
//    public class DefaultSocket : ISocket, IDisposable
//    {
//        private const int DEFAULT_HEARTBEAT_INTERVAL_MS = 10000;
//        private const int DEFAULT_SEND_TIMEOUT_MS = 10000;

//        public bool IsConnected => _adapter.IsOpen;
//        public bool IsConnecting { get; private set; }
//        public int HeartbeatIntervalMs { get; set; }
//        public int SendTimeoutMs { get; set; }

//        public event EventHandler OnDisconnected;
//        public event EventHandler<Exception> OnError;
//        public event EventHandler<ApiNotification> OnNotification;
//        public event EventHandler<ApiChannelMessage> OnChannelMessage;
//        public event EventHandler<ChannelPresenceEvent> OnChannelPresence;
//        //public event EventHandler<StatusPresenceEvent> OnStatusPresence;
//        // ... ALL other events would be declared here ...

//        private readonly string _host;
//        private readonly int _port;
//        private readonly bool _useSsl;
//        private readonly IWebSocketAdapter _adapter;
//        private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _cids = new ConcurrentDictionary<string, TaskCompletionSource<JsonElement>>();
//        private long _nextCid = 1;

//        public DefaultSocket(string host, int port, bool useSsl, IWebSocketAdapter adapter)
//        {
//            _host = host;
//            _port = port;
//            _useSsl = useSsl;
//            _adapter = adapter;
//            HeartbeatIntervalMs = DEFAULT_HEARTBEAT_INTERVAL_MS;
//            SendTimeoutMs = DEFAULT_SEND_TIMEOUT_MS;

//            _adapter.OnClosed += () => OnDisconnected?.Invoke(this, EventArgs.Empty);
//            _adapter.OnError += (ex) => OnError?.Invoke(this, ex);
//            _adapter.OnReceived += OnAdapterReceived;
//        }

//        public async Task ConnectAsync(ISession session, bool appearOnline = true, int connectTimeoutMs = 5000)
//        {
//            if (IsConnected) return;
//            if (IsConnecting) throw new InvalidOperationException("Socket is already connecting.");

//            IsConnecting = true;
//            try
//            {
//                var scheme = _useSsl ? "wss" : "ws";
//                var uriBuilder = new UriBuilder(scheme, _host, _port, "/ws");
//                var query = HttpUtility.ParseQueryString(string.Empty);
//                query["token"] = session.AuthToken;
//                query["status"] = appearOnline.ToString().ToLower();
//                query["lang"] = "en"; // As per JS client
//                uriBuilder.Query = query.ToString();

//                using var cts = new CancellationTokenSource(connectTimeoutMs);
//                await _adapter.ConnectAsync(uriBuilder.Uri, cts.Token);

//                _ = Task.Run(HeartbeatLoop);
//            }
//            finally
//            {
//                IsConnecting = false;
//            }
//        }

//        public async Task DisconnectAsync(bool fireAndForget = false)
//        {
//            if (!IsConnected) return;
//            using var cts = new CancellationTokenSource(SendTimeoutMs);
//            await _adapter.CloseAsync(cts.Token);
//            if (!fireAndForget)
//            {
//                OnDisconnected?.Invoke(this, EventArgs.Empty);
//            }
//        }

//        private void OnAdapterReceived(byte[] data)
//        {
//            var json = Encoding.UTF8.GetString(data);
//            var root = JsonDocument.Parse(json).RootElement;

//            if (root.TryGetProperty("cid", out var cidElement))
//            {
//                var cid = cidElement.GetString();
//                if (_cids.TryRemove(cid, out var tcs))
//                {
//                    if (root.TryGetProperty("error", out var errorElement))
//                    {
//                        var error = errorElement.Deserialize<SocketError>(Json.SerializerOptions);
//                        tcs.SetException(new SocketException(error.Message, error.Code));
//                    }
//                    else
//                    {
//                        tcs.SetResult(root);
//                    }
//                }
//            }
//            else
//            {
//                // This is a server-pushed event, dispatch it
//                DispatchServerMessage(root);
//            }
//        }

//        private void DispatchServerMessage(JsonElement root)
//        {
//            if (root.TryGetProperty("notifications", out var notifications))
//            {
//                var list = notifications.Deserialize<ApiNotificationList>(Json.SerializerOptions);
//                foreach (var n in list.Notifications) OnNotification?.Invoke(this, n);
//            }
//            else if (root.TryGetProperty("channel_message", out var channelMessage))
//            {
//                var msg = channelMessage.Deserialize<ApiChannelMessage>(Json.SerializerOptions);
//                OnChannelMessage?.Invoke(this, msg);
//            }
//            else if (root.TryGetProperty("channel_presence_event", out var presenceEvent))
//            {
//                var pres = presenceEvent.Deserialize<ChannelPresenceEvent>(Json.SerializerOptions);
//                OnChannelPresence?.Invoke(this, pres);
//            }
//            else if (root.TryGetProperty("status_presence_event", out var statusEvent))
//            {
//                //var status = statusEvent.Deserialize<StatusPresenceEvent>(Json.SerializerOptions);
//                //OnStatusPresence?.Invoke(this, status);
//            }
//            // ... and so on for all ~50 event types ...
//        }

//        private async Task<T> SendAsync<T>(object payload) where T : class
//        {
//            if (!IsConnected) throw new InvalidOperationException("Socket is not connected.");

//            var tcs = new TaskCompletionSource<JsonElement>();
//            var cid = Interlocked.Increment(ref _nextCid).ToString();
//            _cids[cid] = tcs;

//            // Using Utf8JsonWriter to manually construct the JSON to add the CID
//            var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();
//            using (var writer = new Utf8JsonWriter(bufferWriter))
//            {
//                var element = JsonDocument.Parse(Json.Serialize(payload)).RootElement;
//                writer.WriteStartObject();
//                writer.WriteString("cid", cid);
//                foreach (var prop in element.EnumerateObject())
//                {
//                    prop.WriteTo(writer);
//                }
//                writer.WriteEndObject();
//            }

//            using var cts = new CancellationTokenSource(SendTimeoutMs);
//            await _adapter.SendAsync(bufferWriter.WrittenMemory.ToArray(), cts.Token);

//            var resultElement = await tcs.Task;
//            // The result will have the a top-level property matching the payload type.
//            var resultProp = resultElement.EnumerateObject().First(p => p.Name != "cid");
//            return resultProp.Value.Deserialize<T>(Json.SerializerOptions);
//        }

//        public async Task<Channel> JoinChatAsync(string target, int type, bool persistence = true, bool hidden = false)
//        {
//            var payload = new { channel_join = new { channel_id = target, type = type, persistence = persistence, hidden = hidden } };
//            return await SendAsync<Channel>(payload);
//        }

//        public async Task LeaveChatAsync(string channelId)
//        {
//            var payload = new { channel_leave = new { channel_id = channelId } };
//            await SendAsync<object>(payload); // No return value for leave
//        }

//        public async Task<ChannelMessageAck> WriteChatMessageAsync(string channelId, string content)
//        {
//            var payload = new { channel_message_send = new { channel_id = channelId, content = content } };
//            return await SendAsync<ChannelMessageAck>(payload);
//        }

//        // ... Implement ALL other ISocket methods similarly ...
//        public Task<ChannelMessageAck> UpdateChatMessageAsync(string channelId, string messageId, string content) => throw new NotImplementedException();
//        public Task<ChannelMessageAck> RemoveChatMessageAsync(string channelId, string messageId) => throw new NotImplementedException();
//        public Task<ApiRpc> RpcAsync(string id, string payload) => throw new NotImplementedException();
//        public Task FollowUsersAsync(IEnumerable<string> userIds) => throw new NotImplementedException();
//        public Task UnfollowUsersAsync(IEnumerable<string> userIds) => throw new NotImplementedException();
//        public Task UpdateStatusAsync(string status) => throw new NotImplementedException();

//        private async Task HeartbeatLoop()
//        {
//            while (IsConnected)
//            {
//                await Task.Delay(HeartbeatIntervalMs);
//                if (!IsConnected) break;

//                try
//                {
//                    await SendAsync<object>(new { ping = new { } });
//                }
//                catch (Exception e)
//                {
//                    OnError?.Invoke(this, e);
//                    await DisconnectAsync(true);
//                    break;
//                }
//            }
//        }

//        public void Dispose()
//        {
//            _adapter?.Dispose();
//        }

//        // Define nested private classes for deserialization that match the socket envelope structure
//        private class SocketError
//        {
//            [JsonPropertyName("code")] public int Code { get; set; }
//            [JsonPropertyName("message")] public string Message { get; set; }
//        }
//    }

//    public class SocketException : Exception
//    {
//        public int Code { get; }
//        public SocketException(string message, int code) : base(message) => Code = code;
//    }
//}
