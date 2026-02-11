using System.Threading.Tasks;
using Mezon.NET.Abstractions;
using Mezon.NET.Logging;
using Mezon.Protobuf.Api;

namespace Mezon.NET.Api
{
    public class MezonClient : BaseMezonClient, IMezonClient, IApiClientProvider
    {
        /// <inheritdoc />
        public MezonClient() : this(new MezonApiClientConfiguration())
        {
        }

        /// <summary>
        ///     Initializes a new <see cref="MezonClient"/> with the provided configuration.
        /// </summary>
        /// <param name="mezonConfiguration">The configuration to be used with the client.</param>
        public MezonClient(MezonApiClientConfiguration mezonConfiguration) : base(mezonConfiguration, CreateApiClient(mezonConfiguration))
        {
        }

        /// <summary>
        ///     Initializes a new <see cref="MezonClient"/> with the provided configuration.
        /// </summary>
        /// <param name="mezonConfiguration">The configuration to be used with the client.</param>
        public MezonClient(MezonApiClientConfiguration mezonConfiguration, IMezonApiClient apiClient) : base(mezonConfiguration, apiClient)
        {
        }

        /// <summary>
        ///     Initializes a new <see cref="MezonClient"/> with the provided configuration and external LogManager.
        /// </summary>
        /// <param name="mezonConfiguration">The configuration to be used with the client.</param>
        /// <param name="logManager">An external LogManager instance for centralized logging.</param>
        public MezonClient(MezonApiClientConfiguration mezonConfiguration, LogManager logManager) : base(mezonConfiguration, CreateApiClient(mezonConfiguration), logManager)
        {
        }

        /// <summary>
        ///     Initializes a new <see cref="MezonClient"/> with the provided configuration, API client, and external LogManager.
        /// </summary>
        /// <param name="mezonConfiguration">The configuration to be used with the client.</param>
        /// <param name="apiClient">The API client to use for requests.</param>
        /// <param name="logManager">An external LogManager instance for centralized logging.</param>
        public MezonClient(MezonApiClientConfiguration mezonConfiguration, IMezonApiClient apiClient, LogManager logManager) : base(mezonConfiguration, apiClient, logManager)
        {
        }

        private static IMezonApiClient CreateApiClient(MezonApiClientConfiguration config)
            => new MezonApiClient(config.HttpClientProvider, config.GRPCClientProvider, config);

        public async Task<AuthenticationResponse> AuthenticateEmailAsync(string email, string password)
        {
            return await ApiClient.AuthenticateEmailAsync(ClientConfiguration.ServerKey, "", new EmailAuthenticationRequest
            {
                Account = new AccountEmailRequest
                {
                    Email = email,
                    Password = password
                },
            });
        }

        public async Task<LoginIDResponse> CreateQRLoginAsync(LoginIDRequest request)
        {
            return await ApiClient.CreateQRLoginAsync(ClientConfiguration.ServerKey, "", request);
        }

        public Task<ClanDescList> GetClanDescriptionAsync(PaginationParams paginationParams)
        {
            return ApiClient.ListClanDescsAsync(paginationParams);
        }

        IMezonApiClient IApiClientProvider.MezonApiClient => ApiClient;
    }
}
