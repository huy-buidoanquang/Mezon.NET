//using System;
//using System.Net.WebSockets;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Mezon.NET.Socket
//{
//    public class WebSocketAdapterText : IWebSocketAdapter
//    {
//        public event Action<byte[]>? OnReceived;
//        public event Action? OnClosed;
//        public event Action<Exception>? OnError;

//        private ClientWebSocket? _ws;
//        private CancellationTokenSource? _cancellationTokenSource;

//        public bool IsOpen => _ws?.State == WebSocketState.Open;

//        public async Task ConnectAsync(Uri uri, CancellationToken ct)
//        {
//            if (IsOpen) return;

//            _cancellationTokenSource = new CancellationTokenSource();
//            _ws = new ClientWebSocket();
//            await _ws.ConnectAsync(uri, ct);
//            _ = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token));
//        }

//        public async Task CloseAsync(CancellationToken ct)
//        {
//            if (_ws != null && _ws.State == WebSocketState.Open)
//            {
//                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
//            }
//            _cancellationTokenSource?.Cancel();
//            _ws?.Dispose();
//            OnClosed?.Invoke();
//        }

//        public Task SendAsync(byte[] buffer, CancellationToken ct)
//        {
//            if (!IsOpen) throw new InvalidOperationException("Socket is not open.");
//            return _ws!.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, ct);
//        }

//        private async Task ReceiveLoop(CancellationToken ct)
//        {
//            var buffer = new byte[1024 * 4];
//            while (!ct.IsCancellationRequested && IsOpen)
//            {
//                try
//                {
//                    var result = await _ws!.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
//                    if (result.MessageType == WebSocketMessageType.Close)
//                    {
//                        await CloseAsync(ct);
//                        break;
//                    }

//                    // This assumes a single message fits in the buffer. For production, a more robust solution would be needed.
//                    var receivedData = new byte[result.Count];
//                    Array.Copy(buffer, 0, receivedData, 0, result.Count);
//                    OnReceived?.Invoke(receivedData);
//                }
//                catch (Exception e)
//                {
//                    OnError?.Invoke(e);
//                    await CloseAsync(CancellationToken.None);
//                    break;
//                }
//            }
//        }

//        public void Dispose()
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
