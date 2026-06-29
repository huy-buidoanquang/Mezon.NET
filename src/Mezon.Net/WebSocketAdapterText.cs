using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Abstractions.Events;
using Microsoft.Extensions.Logging;

namespace Mezon.NET
{
    internal class WebSocketAdapterText : IWebSocketAdapter, IAsyncDisposable
    {
        private const int DefaultBufferSize = 8192;
        private const string DefaultLanguage = "en";

        private ClientWebSocket? _socket;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _listenTask;
        private bool _disposed;

        private readonly ILogger<IWebSocketAdapter> _logger;

        public event EventHandler<SocketAdapterOpenEventArgs>? Opened;
        public event EventHandler<SocketAdapterCloseEventArgs>? Closed;
        public event EventHandler<SocketAdapterMessageEventArgs>? MessageReceived;
        public event EventHandler<SocketAdapterErrorEventArgs>? ErrorOccurred;

        public WebSocketAdapterText(ILogger<IWebSocketAdapter> logger)
        {
            _logger = logger;
        }

        bool IWebSocketAdapter.IsOpen() => IsOpen();
        public bool IsOpen() => _socket?.State == WebSocketState.Open;

        public async Task ConnectAsync(
            string scheme,
            string host,
            int port,
            bool createStatus,
            string token,
            CancellationToken cancellation = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WebSocketAdapterText));
            }

            if (IsOpen())
            {
                return;
            }

            await CloseInternalAsync(cancellation).ConfigureAwait(false);

            var url = BuildWebSocketUrl(scheme, host, port, createStatus, token);
            _socket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await _socket.ConnectAsync(url, cancellation).ConfigureAwait(false);
                Opened?.Invoke(this, new SocketAdapterOpenEventArgs(_socket));

                var combinedToken = CancellationTokenSource
                    .CreateLinkedTokenSource(cancellation, _cancellationTokenSource.Token)
                    .Token;

                _listenTask = Task.Run(() => ListenAsync(combinedToken), combinedToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to WebSocket.");
                ErrorOccurred?.Invoke(this, new SocketAdapterErrorEventArgs(ex, ex.Message, _socket));
                await CloseInternalAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        public async Task CloseAsync(CancellationToken cancellation = default)
        {
            await CloseInternalAsync(cancellation).ConfigureAwait(false);
        }

        public async Task SendAsync(object message, CancellationToken cancellation = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WebSocketAdapterText));
            }

            if (!IsOpen() || _socket is null)
            {
                throw new InvalidOperationException("WebSocket is not connected.");
            }

            try
            {
                var rootNode = JsonSerializer.SerializeToNode(message);
                if (!(rootNode is JsonObject rootObject))
                {
                    await _socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, true, cancellation).ConfigureAwait(false);
                    return;
                }

                if (rootObject.TryGetPropertyValue("party_data_send", out var partyDataSendNode)
                    && partyDataSendNode is JsonObject partyDataSend)
                {
                    if (partyDataSend.TryGetPropertyValue("op_code", out var opCodeNode) && !(opCodeNode is null))
                    {
                        partyDataSend["op_code"] = opCodeNode.ToString();
                    }

                    if (partyDataSend.TryGetPropertyValue("data", out var dataNode)
                        && dataNode is JsonValue jsonValue
                        && jsonValue.TryGetValue<string>(out var dataString))
                    {
                        var bytesToEncode = Encoding.UTF8.GetBytes(dataString);
                        partyDataSend["data"] = Convert.ToBase64String(bytesToEncode);
                    }
                }

                var finalJson = rootObject.ToJsonString();
                var buffer = Encoding.UTF8.GetBytes(finalJson);
                await _socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellation).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message over WebSocket.");
                ErrorOccurred?.Invoke(this, new SocketAdapterErrorEventArgs(ex, ex.Message, _socket));
                throw;
            }
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            try
            {
                if (_socket is null)
                {
                    return;
                }

                var bufferSegment = new ArraySegment<byte>(buffer);

                while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var message = await ReceiveCompleteMessageAsync(_socket, bufferSegment, cancellationToken).ConfigureAwait(false);
                    if (message is null)
                    {
                        break;
                    }

                    ProcessReceivedMessage(message);
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning("WebSocket listening task was canceled.");
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error occurred.");
                ErrorOccurred?.Invoke(this, new SocketAdapterErrorEventArgs(ex, ex.Message, _socket));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in WebSocket listening task.");
                ErrorOccurred?.Invoke(this, new SocketAdapterErrorEventArgs(ex, ex.Message, _socket));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                OnConnectionClosed();
            }
        }

        private static async Task<string?> ReceiveCompleteMessageAsync(WebSocket socket, ArraySegment<byte> buffer, CancellationToken token)
        {
            await using var ms = new MemoryStream();
            WebSocketReceiveResult socketReceiveResult;

            do
            {
                socketReceiveResult = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);

                if (socketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (buffer.Array != null)
                {
                    ms.Write(buffer.Array, buffer.Offset, socketReceiveResult.Count);
                }
            } while (!socketReceiveResult.EndOfMessage);

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private void ProcessReceivedMessage(string messageString)
        {
            try
            {
                var messageNode = JsonNode.Parse(messageString);
                byte[]? decodedData = null;

                if (messageNode is JsonObject messageObject
                    && messageObject.TryGetPropertyValue("party_data", out var partyDataNode)
                    && partyDataNode is JsonObject partyData
                    && partyData.TryGetPropertyValue("data", out var dataNode)
                    && dataNode is JsonValue jsonValue
                    && jsonValue.TryGetValue<string>(out var base64String))
                {
                    decodedData = Convert.FromBase64String(base64String);
                }

                MessageReceived?.Invoke(this, new SocketAdapterMessageEventArgs(messageNode, decodedData, _socket));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing received WebSocket message.");
                ErrorOccurred?.Invoke(this, new SocketAdapterErrorEventArgs(ex, "Failed to parse or process message.", _socket));
            }
        }

        private void OnConnectionClosed()
        {
            if (_socket is null)
            {
                return;
            }

            var wasClean = _socket.CloseStatus.HasValue;
            var closeCode = (int)(_socket.CloseStatus ?? WebSocketCloseStatus.Empty);
            var closeReason = _socket.CloseStatusDescription ?? string.Empty;

            Closed?.Invoke(this, new SocketAdapterCloseEventArgs(wasClean, closeCode, closeReason, _socket));
        }

        private async Task CloseInternalAsync(CancellationToken cancellation = default)
        {
            if (_cancellationTokenSource is { IsCancellationRequested: false })
            {
                _cancellationTokenSource.Cancel();
            }

            if (_listenTask != null)
            {
                try
                {
                    await _listenTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Listen task was cancelled during close.");
                }
            }

            if (_socket?.State == WebSocketState.Open)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellation);
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", linkedCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while closing WebSocket");
                    ErrorOccurred?.Invoke(this, new SocketAdapterErrorEventArgs(ex, ex.Message, _socket));
                }
            }

            _socket?.Dispose();
            _cancellationTokenSource?.Dispose();

            _socket = null;
            _cancellationTokenSource = null;
            _listenTask = null;
        }

        private static Uri BuildWebSocketUrl(string scheme, string host, int port, bool createStatus, string token)
        {
            var status = createStatus ? "true" : "false";
            var escapedToken = Uri.EscapeDataString(token);
            var builder = new UriBuilder(scheme, host, port, "/ws")
            {
                Query = $"lang={DefaultLanguage}&status={status}&token={escapedToken}"
            };
            return builder.Uri;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await CloseInternalAsync(CancellationToken.None).ConfigureAwait(false);

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
