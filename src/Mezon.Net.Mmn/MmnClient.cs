using Mezon.Net.Mmn.Models;

namespace Mezon.Net.Mmn
{
    public sealed class MmnClient : IDisposable
    {
        private bool _disposed;

        public MmnClient(MmnConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            NodeClient = new MmnNodeClient(config.Endpoint);
            ZkProveClient = new ZkProveClient(config.ZkProveEndpoint, config.TimeoutMs);
        }

        public MmnNodeClient NodeClient { get; }

        public ZkProveClient ZkProveClient { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            NodeClient.Dispose();
            ZkProveClient.Dispose();
            _disposed = true;
        }
    }
}
