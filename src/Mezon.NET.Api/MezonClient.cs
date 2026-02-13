using System.Threading.Tasks;
using Mezon.NET.Abstractions;
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

        private static IMezonApiClient CreateApiClient(MezonApiClientConfiguration config)
            => new MezonApiClient(config.HttpClientProvider, config.GRPCClientProvider, config);

        public async Task<AuthenticationResponse> AuthenticateEmailAsync(string email, string password)
        {
            return await ApiClient.AuthenticateEmailAsync(Configuration.ServerKey, "", new EmailAuthenticationRequest
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
            return await ApiClient.CreateQRLoginAsync(Configuration.ServerKey, "", request);
        }

        public Task<ClanDescList> GetClanDescriptionAsync(PaginationParams paginationParams)
        {
            return ApiClient.ListClanDescsAsync(paginationParams);
        }

        IMezonClient IApiClientProvider.MezonApiClient => this;
    }
}
