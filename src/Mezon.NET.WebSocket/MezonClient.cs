using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Api;
using Mezon.NET.Core;
using Mezon.NET.Logging;
using static Mezon.Protobuf.Api.Friend.Types;

namespace Mezon.NET.WebSocket
{
    public partial class MezonClient : BaseSocketClient, IMezonClient
    {
        private readonly SocketConnectionManager _connection;
        private readonly SemaphoreSlim _stateLock;
        private readonly Logger _socketLogger;

        internal int? HandlerTimeout { get; private set; }

        public override Api.MezonClient RestClient { get; }

        public MezonClient() : this(new MezonSocketClientConfiguration())
        {
        }

        public MezonClient(MezonSocketClientConfiguration configuration) : this(configuration, CreateSocketApiClient(configuration))
        {
        }

        public MezonClient(MezonSocketClientConfiguration configuration, IMezonSocketClient socketClient) : base(configuration, socketClient)
        {
            _stateLock = new SemaphoreSlim(1);
            _socketLogger = LogManager.CreateLogger("MezonSocketClient");
            RestClient = new Api.MezonClient(configuration, ApiClient);
            HandlerTimeout = configuration.HandlerTimeout;

            RestClient = new Api.MezonClient(configuration, ApiClient);
            _connection = new SocketConnectionManager(
                _stateLock,
                _socketLogger, configuration.ConnectionTimeout,
               OnConnectingAsync, OnDisconnectingAsync, x => ApiClient.Disconnected += x);
            _connection.Connected += () => TimedInvokeAsync(_connectedEvent, nameof(Connected));
            _connection.Disconnected += (ex, recon) => TimedInvokeAsync(_disconnectedEvent, nameof(Disconnected), ex);
        }

        private static MezonSocketApiClient CreateSocketApiClient(MezonSocketClientConfiguration configuration)
            => new MezonSocketApiClient(configuration.HttpClientProvider, configuration.GRPCClientProvider, configuration.WebSocketClientProvider, configuration);

        private async Task OnConnectingAsync() => await Task.CompletedTask;
        private async Task OnDisconnectingAsync(Exception ex) => await Task.CompletedTask;

        private Task TimedInvokeAsync(AsyncEvent<Func<Task>> eventHandler, string name)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue ? TimeoutWrap(name, eventHandler.InvokeAsync) : eventHandler.InvokeAsync();
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T>(AsyncEvent<Func<T, Task>> eventHandler, string name, T arg)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg)) : eventHandler.InvokeAsync(arg);
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T1, T2>(AsyncEvent<Func<T1, T2, Task>> eventHandler, string name, T1 arg1, T2 arg2)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2)) : eventHandler.InvokeAsync(arg1, arg2);
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T1, T2, T3>(AsyncEvent<Func<T1, T2, T3, Task>> eventHandler, string name, T1 arg1, T2 arg2, T3 arg3)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue
                    ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2, arg3))
                    : eventHandler.InvokeAsync(arg1, arg2, arg3);
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T1, T2, T3, T4>(AsyncEvent<Func<T1, T2, T3, T4, Task>> eventHandler, string name, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue
                    ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2, arg3, arg4))
                    : eventHandler.InvokeAsync(arg1, arg2, arg3, arg4);
            }

            return Task.CompletedTask;
        }

        private Task TimedInvokeAsync<T1, T2, T3, T4, T5>(AsyncEvent<Func<T1, T2, T3, T4, T5, Task>> eventHandler, string name, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (eventHandler.HasSubscribers)
            {
                return HandlerTimeout.HasValue
                    ? TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2, arg3, arg4, arg5))
                    : eventHandler.InvokeAsync(arg1, arg2, arg3, arg4, arg5);
            }

            return Task.CompletedTask;
        }

        private async Task TimeoutWrap(string name, Func<Task> action)
        {
            try
            {
                if (!HandlerTimeout.HasValue)
                {
                    return;
                }

                var timeoutTask = Task.Delay(HandlerTimeout.Value);
                var handlersTask = action();
                if (await Task.WhenAny(timeoutTask, handlersTask).ConfigureAwait(false) == timeoutTask)
                {
                    await _socketLogger.WarningAsync($"A {name} handler is blocking the gateway task.").ConfigureAwait(false);
                }
                await handlersTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _socketLogger.WarningAsync($"A {name} handler has thrown an unhandled exception.", ex).ConfigureAwait(false);
            }
        }

        public async Task ConnectAsync()
        {
            await ApiClient.ConnectAsync();
        }

        public async Task DisconnectAsync()
        {
            await ApiClient.DisconnectAsync();
        }
    }
}
