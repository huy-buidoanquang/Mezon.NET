using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Mezon.Net.Core
{
    public class AsyncEvent<T>
        where T : class
    {
        private readonly object _subLock = new object();
        internal ImmutableArray<T> _subscriptions;

        public bool HasSubscribers => _subscriptions.Length != 0;
        public IReadOnlyList<T> Subscriptions => _subscriptions;

        public AsyncEvent()
        {
            _subscriptions = ImmutableArray.Create<T>();
        }

        public void Add(T subscriber)
        {
            Check.NotNull(subscriber, nameof(subscriber));
            lock (_subLock)
            {
                _subscriptions = _subscriptions.Add(subscriber);
            }
        }
        public void Remove(T subscriber)
        {
            Check.NotNull(subscriber, nameof(subscriber));
            lock (_subLock)
            {
                _subscriptions = _subscriptions.Remove(subscriber);
            }
        }
    }

    public static class EventExtensions
    {
        private static void RecordFailure(Exception ex, ref Exception? firstFailure, ref List<Exception>? moreFailures)
        {
            if (firstFailure is null)
            {
                firstFailure = ex;
                return;
            }

            (moreFailures ??= new List<Exception>()).Add(ex);
        }

        /// <summary>
        /// Every subscriber runs even if an earlier one fails. One failure is rethrown with its original stack trace;
        /// several are reported together as an <see cref="AggregateException"/>.
        /// </summary>
        private static void ThrowFailures(Exception firstFailure, List<Exception>? moreFailures)
        {
            if (moreFailures is null)
            {
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }

            moreFailures!.Insert(0, firstFailure);
            throw new AggregateException(moreFailures);
        }

        public static async Task InvokeAsync(this AsyncEvent<Func<Task>> eventHandler)
        {
            var subscribers = eventHandler._subscriptions;
            Exception? firstFailure = null;
            List<Exception>? moreFailures = null;
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    await subscribers[i].Invoke().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RecordFailure(ex, ref firstFailure, ref moreFailures);
                }
            }

            if (firstFailure is not null)
            {
                ThrowFailures(firstFailure, moreFailures);
            }
        }
        public static async Task InvokeAsync<T>(this AsyncEvent<Func<T, Task>> eventHandler, T arg)
        {
            var subscribers = eventHandler._subscriptions;
            Exception? firstFailure = null;
            List<Exception>? moreFailures = null;
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    await subscribers[i].Invoke(arg).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RecordFailure(ex, ref firstFailure, ref moreFailures);
                }
            }

            if (firstFailure is not null)
            {
                ThrowFailures(firstFailure, moreFailures);
            }
        }
        public static async Task InvokeAsync<T1, T2>(this AsyncEvent<Func<T1, T2, Task>> eventHandler, T1 arg1, T2 arg2)
        {
            var subscribers = eventHandler._subscriptions;
            Exception? firstFailure = null;
            List<Exception>? moreFailures = null;
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    await subscribers[i].Invoke(arg1, arg2).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RecordFailure(ex, ref firstFailure, ref moreFailures);
                }
            }

            if (firstFailure is not null)
            {
                ThrowFailures(firstFailure, moreFailures);
            }
        }
        public static async Task InvokeAsync<T1, T2, T3>(this AsyncEvent<Func<T1, T2, T3, Task>> eventHandler, T1 arg1, T2 arg2, T3 arg3)
        {
            var subscribers = eventHandler._subscriptions;
            Exception? firstFailure = null;
            List<Exception>? moreFailures = null;
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    await subscribers[i].Invoke(arg1, arg2, arg3).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RecordFailure(ex, ref firstFailure, ref moreFailures);
                }
            }

            if (firstFailure is not null)
            {
                ThrowFailures(firstFailure, moreFailures);
            }
        }
        public static async Task InvokeAsync<T1, T2, T3, T4>(this AsyncEvent<Func<T1, T2, T3, T4, Task>> eventHandler, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            var subscribers = eventHandler._subscriptions;
            Exception? firstFailure = null;
            List<Exception>? moreFailures = null;
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    await subscribers[i].Invoke(arg1, arg2, arg3, arg4).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RecordFailure(ex, ref firstFailure, ref moreFailures);
                }
            }

            if (firstFailure is not null)
            {
                ThrowFailures(firstFailure, moreFailures);
            }
        }
        public static async Task InvokeAsync<T1, T2, T3, T4, T5>(this AsyncEvent<Func<T1, T2, T3, T4, T5, Task>> eventHandler, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            var subscribers = eventHandler._subscriptions;
            Exception? firstFailure = null;
            List<Exception>? moreFailures = null;
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    await subscribers[i].Invoke(arg1, arg2, arg3, arg4, arg5).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RecordFailure(ex, ref firstFailure, ref moreFailures);
                }
            }

            if (firstFailure is not null)
            {
                ThrowFailures(firstFailure, moreFailures);
            }
        }
    }
}
