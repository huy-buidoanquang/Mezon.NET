using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Caching
{
    public sealed class ChannelSendQueue
    {
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new ConcurrentDictionary<long, SemaphoreSlim>();

        public async Task<T> EnqueueAsync<T>(long channelId, Func<Task<T>> action)
        {
            var gate = _locks.GetOrAdd(channelId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        public Task EnqueueAsync(long channelId, Func<Task> action)
            => EnqueueAsync(channelId, async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            });
    }
}
