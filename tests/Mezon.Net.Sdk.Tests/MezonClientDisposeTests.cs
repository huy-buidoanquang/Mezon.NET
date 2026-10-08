using System.Threading.Tasks;
using Mezon.Net.Sdk;
using Xunit;

namespace Mezon.Net.Sdk.Tests
{
    public sealed class MezonClientDisposeTests
    {
        [Fact]
        public async Task Disposing_twice_does_not_throw()
        {
            // `await using` plus an explicit DisposeAsync, or a DI container disposing a client the app already
            // disposed, must be harmless.
            var client = new MezonClient(new MezonClientOptions(1, "token"));

            await client.DisposeAsync();
            await client.DisposeAsync();
        }
    }
}
