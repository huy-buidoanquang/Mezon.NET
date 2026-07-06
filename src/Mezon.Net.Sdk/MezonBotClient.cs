using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk
{
    [Obsolete("Use Mezon.Net.Sdk.MezonClient instead.")]
    public sealed class MezonBotClient : IDisposable, IAsyncDisposable
    {
        private readonly MezonClient _client;

        public MezonBotClient() : this(new MezonClientOptions())
        {
        }

        public MezonBotClient(MezonClientOptions options)
        {
            _client = new MezonClient(options);
        }

        public MezonClient InnerClient => _client;

        public Task<bool> LoginAsync(CancellationToken cancellationToken = default)
            => _client.LoginAsync(cancellationToken);

        public void Dispose() => _client.DisposeAsync().AsTask().GetAwaiter().GetResult();

        public ValueTask DisposeAsync() => _client.DisposeAsync();
    }
}
