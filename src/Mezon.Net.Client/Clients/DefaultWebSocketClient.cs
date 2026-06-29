using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Client
{
    internal partial class DefaultWebSocketClient : IWebSocketClient, IDisposable, IAsyncDisposable
    {
        public const int InitialBufferSize = 16 * 1024; //16KB
        public const int SendChunkSize = 4 * 1024; //4KB
        private const int HR_TIMEOUT = -2147012894;
        private const int WS_CONNECTING_RETRY = 10;

        public event Func<ReadOnlyMemory<byte>, ValueTask>? MessageReceived;
        public event Func<Task>? Opened;
        public event Func<Task>? Ready;
        public event Func<Exception, Task>? Closed;
        public event Func<Exception, Task>? ErrorOccurred;

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        private readonly IWebProxy? _proxy;
        private ClientWebSocket? _client;
        private Task? _receiveTask;
        private CancellationTokenSource? _disconnectTokenSource, _cancelTokenSource;
        private CancellationToken _cancelToken, _parentToken;
        private bool _isDisposed, _isDisconnecting;

        public DefaultWebSocketClient(IWebProxy? webProxy = null)
        {
            _proxy = webProxy;
            _disconnectTokenSource = new CancellationTokenSource();
            _cancelToken = CancellationToken.None;
            _parentToken = CancellationToken.None;
        }

        public async Task ConnectAsync(string host)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await ConnectInternalAsync(host).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task ConnectInternalAsync(string host)
        {
            await DisconnectInternalAsync().ConfigureAwait(false);

            _disconnectTokenSource?.Dispose();
            _cancelTokenSource?.Dispose();

            _disconnectTokenSource = new CancellationTokenSource();
            _cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_parentToken, _disconnectTokenSource.Token);
            _cancelToken = _cancelTokenSource.Token;

            _client = new ClientWebSocket();
            _client.Options.Proxy = _proxy;
            _client.Options.KeepAliveInterval = TimeSpan.Zero;
            foreach (var header in _headers)
            {
                if (header.Value != null)
                {
                    _client.Options.SetRequestHeader(header.Key, header.Value);
                }
            }

            await _client.ConnectAsync(new Uri(host), _cancelToken).ConfigureAwait(false);
            await OnOpened().ConfigureAwait(false);
            _receiveTask = ReceiveLoopAsync(_cancelToken);
            await OnReady().ConfigureAwait(false);
        }

        public async Task DisconnectAsync(int closeCode = 1000)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisconnectInternalAsync(closeCode).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task DisconnectInternalAsync(int closeCode = 1000, bool isDisposing = false)
        {
            _isDisconnecting = true;

            if (_disconnectTokenSource != null)
            {
                try
                {
                    _disconnectTokenSource.Cancel(false);
                    _disconnectTokenSource.Dispose();
                    _disconnectTokenSource = null;
                }
                catch { }
            }

            if (_client != null)
            {
                if (!isDisposing)
                {
                    try
                    {
                        if (_client.State == WebSocketState.Open)
                        {
                            await _client.CloseOutputAsync((WebSocketCloseStatus)closeCode, "", CancellationToken.None);
                        }
                    }
                    catch { }
                }

                try
                {
                    _client.Dispose();
                }
                catch { }

                _client = null;
            }

            try
            {
                await (_receiveTask ?? Task.CompletedTask).ConfigureAwait(false);
                _receiveTask = null;
            }
            finally
            {
                _isDisconnecting = false;
            }
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> data)
        {
            if (_client == null)
            {
                return;
            }

            int retry = 0;
            while (_client.State == WebSocketState.Connecting && retry < WS_CONNECTING_RETRY)
            {
                await Task.Delay(100);
                retry++;
            }

            if (_client.State != WebSocketState.Open)
            {
                return;
            }

            try
            {
                await _lock.WaitAsync(_cancelToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            try
            {
                await _client.SendAsync(data, WebSocketMessageType.Binary, true, _cancelToken).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public void SetHeader(string key, string value)
        {
            _headers[key] = value;
        }

        public void SetCancelToken(CancellationToken cancelToken)
        {
            _cancelTokenSource?.Dispose();
            _parentToken = cancelToken;
            _cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_parentToken, _disconnectTokenSource?.Token ?? CancellationToken.None);
            _cancelToken = _cancelTokenSource.Token;
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
            int tBytesReceived = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    ValueWebSocketReceiveResult result = await _client!
                        .ReceiveAsync(new Memory<byte>(buffer, tBytesReceived, buffer.Length - tBytesReceived), token)
                        .ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (Closed != null)
                        {
                            var ex = new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "Remote closed connection");
                            await OnClosed(ex).ConfigureAwait(false);
                        }
                        break;
                    }

                    tBytesReceived += result.Count;

                    if (result.EndOfMessage)
                    {
                        await OnMesasageReceived(buffer, tBytesReceived).ConfigureAwait(false);
                        tBytesReceived = 0;
                    }
                    else
                    {
                        if (tBytesReceived >= buffer.Length)
                        {
                            int newSize = buffer.Length * 2;
                            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
                            Buffer.BlockCopy(buffer, 0, newBuffer, 0, tBytesReceived);
                            ArrayPool<byte>.Shared.Return(buffer);
                            buffer = newBuffer;
                        }
                    }
                }
            }
            catch (Win32Exception ex) when (ex.HResult == HR_TIMEOUT)
            {
                var _ = OnClosed(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "Connection timed out.", ex));
            }
            catch (OperationCanceledException) { /* Normal shutdown */ }
            catch (Exception ex)
            {
                await OnErrorOccurred(ex).ConfigureAwait(false);
                await OnClosed(ex).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                await DisconnectInternalAsync().ConfigureAwait(false);
            }
        }

        #region Event invokers
        private async Task OnMesasageReceived(byte[] bytes, int length)
        {
            if (MessageReceived != null)
            {
                await MessageReceived.Invoke(new ReadOnlyMemory<byte>(bytes, 0, length)).ConfigureAwait(false);
            }
        }

        private async Task OnClosed(Exception ex)
        {
            if (_isDisconnecting)
            {
                return;
            }

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisconnectInternalAsync(isDisposing: false);
            }
            finally
            {
                _lock.Release();
            }
            if (Closed != null)
            {
                await Closed.Invoke(ex).ConfigureAwait(false);
            }
        }

        private async Task OnErrorOccurred(Exception ex)
        {
            if (_isDisconnecting)
            {
                return;
            }

            if (ErrorOccurred != null)
            {
                await ErrorOccurred.Invoke(ex).ConfigureAwait(false);
            }
        }

        private async Task OnOpened()
        {
            if (Opened != null)
            {
                await Opened.Invoke().ConfigureAwait(false);
            }
        }

        private async Task OnReady()
        {
            if (Ready != null)
            {
                await Ready.Invoke().ConfigureAwait(false);
            }
        }
        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectInternalAsync().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _lock.Dispose();
                    _disconnectTokenSource?.Dispose();
                    _cancelTokenSource?.Dispose();
                    _client?.Dispose();
                }
                _isDisposed = true;
            }
        }
    }
}
