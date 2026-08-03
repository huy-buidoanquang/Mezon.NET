using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Entities;
using Mezon.Net.Sdk.Interactions;

namespace Mezon.Net.Sdk.Collectors
{
    internal interface ICollectorSession
    {
        bool IsDisposed { get; }
        void Dispose();
    }

    internal abstract class CollectorSessionBase : ICollectorSession
    {
        private int _disposed;

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            OnDispose();
        }

        protected abstract void OnDispose();
    }

    internal sealed class MessageCollectorSession : CollectorSessionBase
    {
        private readonly MessageCollectorOptions _options;
        private readonly TaskCompletionSource<MessageCollectorResult> _tcs;
        private readonly List<Message> _messages = new List<Message>();
        private readonly CancellationTokenRegistration _cancellationRegistration;
        private readonly Timer? _timeoutTimer;
        private Timer? _idleTimer;
        private readonly object _gate = new object();
        private readonly Action<MessageCollectorSession> _unregister;

        public MessageCollectorSession(
            MessageCollectorOptions options,
            CancellationToken cancellationToken,
            Action<MessageCollectorSession> unregister)
        {
            _options = options;
            _unregister = unregister;
            _tcs = new TaskCompletionSource<MessageCollectorResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.CanBeCanceled)
            {
                _cancellationRegistration = cancellationToken.Register(static state =>
                {
                    var session = (MessageCollectorSession)state!;
                    session.Complete(CollectorStatus.Cancelled);
                }, this);
            }

            if (options.Timeout is TimeSpan timeout && timeout > TimeSpan.Zero)
            {
                _timeoutTimer = new Timer(
                    static state => ((MessageCollectorSession)state!).Complete(CollectorStatus.TimedOut),
                    this,
                    timeout,
                    Timeout.InfiniteTimeSpan);
            }
        }

        public Task<MessageCollectorResult> Task => _tcs.Task;

        internal void Cancel() => Complete(CollectorStatus.Cancelled);

        public bool TryCollect(Message message)
        {
            if (IsDisposed)
            {
                return false;
            }

            lock (_gate)
            {
                if (IsDisposed)
                {
                    return false;
                }

                if (_options.ChannelId is long channelId && message.ChannelId != channelId)
                {
                    return false;
                }

                if (_options.UserId is long userId && message.SenderId != userId)
                {
                    return false;
                }

                if (_options.MessageId is long messageId && message.Id != messageId)
                {
                    return false;
                }

                if (_options.Filter is not null && !_options.Filter(message))
                {
                    return false;
                }

                _messages.Add(message);
                ResetIdleTimerLocked();

                if (_messages.Count >= Math.Max(1, _options.Max))
                {
                    CompleteLocked(CollectorStatus.Collected);
                    return true;
                }

                return true;
            }
        }

        private void ResetIdleTimerLocked()
        {
            if (_options.IdleTimeout is not TimeSpan idleTimeout || idleTimeout <= TimeSpan.Zero)
            {
                return;
            }

            _idleTimer ??= new Timer(
                static state => ((MessageCollectorSession)state!).Complete(CollectorStatus.TimedOut),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _idleTimer.Change(idleTimeout, Timeout.InfiniteTimeSpan);
        }

        private void Complete(CollectorStatus status)
        {
            lock (_gate)
            {
                CompleteLocked(status);
            }
        }

        private void CompleteLocked(CollectorStatus status)
        {
            if (IsDisposed)
            {
                return;
            }

            Dispose();
            if (!_tcs.TrySetResult(CreateResult(status)))
            {
                return;
            }
        }

        private MessageCollectorResult CreateResult(CollectorStatus status)
        {
            if (status != CollectorStatus.Collected || _messages.Count == 0)
            {
                return new MessageCollectorResult(status);
            }

            return _messages.Count == 1
                ? new MessageCollectorResult(status, _messages[0], _messages)
                : new MessageCollectorResult(status, _messages[0], _messages);
        }

        protected override void OnDispose()
        {
            _timeoutTimer?.Dispose();
            _idleTimer?.Dispose();
            _cancellationRegistration.Dispose();
            _unregister(this);
        }
    }

    internal sealed class ComponentCollectorSession : CollectorSessionBase
    {
        private readonly ComponentCollectorOptions _options;
        private readonly TaskCompletionSource<ComponentCollectorResult> _tcs;
        private readonly List<IInteraction> _interactions = new List<IInteraction>();
        private readonly CancellationTokenRegistration _cancellationRegistration;
        private readonly Timer? _timeoutTimer;
        private Timer? _idleTimer;
        private readonly object _gate = new object();
        private readonly Action<ComponentCollectorSession> _unregister;

        public ComponentCollectorSession(
            ComponentCollectorOptions options,
            CancellationToken cancellationToken,
            Action<ComponentCollectorSession> unregister)
        {
            _options = options;
            _unregister = unregister;
            _tcs = new TaskCompletionSource<ComponentCollectorResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.CanBeCanceled)
            {
                _cancellationRegistration = cancellationToken.Register(static state =>
                {
                    var session = (ComponentCollectorSession)state!;
                    session.Complete(CollectorStatus.Cancelled);
                }, this);
            }

            if (options.Timeout is TimeSpan timeout && timeout > TimeSpan.Zero)
            {
                _timeoutTimer = new Timer(
                    static state => ((ComponentCollectorSession)state!).Complete(CollectorStatus.TimedOut),
                    this,
                    timeout,
                    Timeout.InfiniteTimeSpan);
            }
        }

        public Task<ComponentCollectorResult> Task => _tcs.Task;

        internal void Cancel() => Complete(CollectorStatus.Cancelled);

        public bool TryCollect(IInteraction interaction)
        {
            if (IsDisposed)
            {
                return false;
            }

            lock (_gate)
            {
                if (IsDisposed)
                {
                    return false;
                }

                if (_options.ChannelId is long channelId && interaction.ChannelId != channelId)
                {
                    return false;
                }

                if (_options.UserId is long userId && interaction.UserId != userId)
                {
                    return false;
                }

                if (_options.MessageId is long messageId && interaction.MessageId != messageId)
                {
                    return false;
                }

                if (_options.ComponentId is string componentId
                    && !string.Equals(componentId, interaction.CustomId, StringComparison.Ordinal))
                {
                    return false;
                }

                if (_options.Filter is not null && !_options.Filter(interaction))
                {
                    return false;
                }

                _interactions.Add(interaction);
                ResetIdleTimerLocked();

                if (_interactions.Count >= Math.Max(1, _options.Max))
                {
                    CompleteLocked(CollectorStatus.Collected);
                    return true;
                }

                return true;
            }
        }

        private void ResetIdleTimerLocked()
        {
            if (_options.IdleTimeout is not TimeSpan idleTimeout || idleTimeout <= TimeSpan.Zero)
            {
                return;
            }

            _idleTimer ??= new Timer(
                static state => ((ComponentCollectorSession)state!).Complete(CollectorStatus.TimedOut),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _idleTimer.Change(idleTimeout, Timeout.InfiniteTimeSpan);
        }

        private void Complete(CollectorStatus status)
        {
            lock (_gate)
            {
                CompleteLocked(status);
            }
        }

        private void CompleteLocked(CollectorStatus status)
        {
            if (IsDisposed)
            {
                return;
            }

            Dispose();
            _tcs.TrySetResult(CreateResult(status));
        }

        private ComponentCollectorResult CreateResult(CollectorStatus status)
        {
            if (status != CollectorStatus.Collected || _interactions.Count == 0)
            {
                return new ComponentCollectorResult(status);
            }

            return new ComponentCollectorResult(status, _interactions[0], _interactions);
        }

        protected override void OnDispose()
        {
            _timeoutTimer?.Dispose();
            _idleTimer?.Dispose();
            _cancellationRegistration.Dispose();
            _unregister(this);
        }
    }
}
