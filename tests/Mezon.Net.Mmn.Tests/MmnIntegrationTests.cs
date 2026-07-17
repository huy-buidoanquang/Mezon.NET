using Mezon.Net.Mmn;
using Mezon.Net.Mmn.Models;
using Xunit;

namespace Mezon.Net.Mmn.Tests
{
    public sealed class MmnIntegrationTests
    {
        private static bool IntegrationEnabled =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MMN_TEST_ENDPOINT"));

        private static MmnClient CreateClient()
        {
            var endpoint = Environment.GetEnvironmentVariable("MMN_TEST_ENDPOINT")
                ?? throw new InvalidOperationException("MMN_TEST_ENDPOINT is not set.");
            var zkEndpoint = Environment.GetEnvironmentVariable("MMN_TEST_ZK_ENDPOINT") ?? endpoint;

            return new MmnClient(new MmnConfig
            {
                Endpoint = endpoint,
                ZkProveEndpoint = zkEndpoint,
            });
        }

        [Fact]
        public async Task CheckHealth_WhenEndpointConfigured_ReturnsResponse()
        {
            if (!IntegrationEnabled)
            {
                return;
            }

            using var client = CreateClient();
            var response = await client.NodeClient.CheckHealthAsync();
            Assert.NotNull(response);
        }

        [Fact]
        public async Task ZkHealthCheck_WhenEndpointConfigured_ReturnsHealthy()
        {
            if (!IntegrationEnabled)
            {
                return;
            }

            using var client = CreateClient();
            var response = await client.ZkProveClient.HealthCheckAsync();
            Assert.False(string.IsNullOrWhiteSpace(response.Status));
        }
    }
}
