using System.Threading.Tasks;
using Mezon.Net.Abstractions;
using Mezon.Net.Internal.Api;

namespace Mezon.Net.Api
{
    public class MezonClient : BaseMezonClient, IMezonClient, IApiClientProvider
    {
        /// <inheritdoc />
        public MezonClient() : this(new MezonApiClientOptions())
        {
        }

        /// <summary>
        ///     Initializes a new <see cref="MezonClient"/> with the provided configuration.
        /// </summary>
        /// <param name="options">The configuration to be used with the client.</param>
        public MezonClient(MezonApiClientOptions options) : base(options, CreateApiClient(options))
        {
        }

        /// <summary>
        ///     Initializes a new <see cref="MezonClient"/> with the provided configuration.
        /// </summary>
        /// <param name="mezonConfiguration">The configuration to be used with the client.</param>
        public MezonClient(MezonApiClientOptions options, IMezonApiClient apiClient) : base(options, apiClient)
        {
        }

        private static IMezonApiClient CreateApiClient(MezonApiClientOptions options)
            => new MezonApiClient(options.HttpClientProvider, options.NetworkTransportProvider, options);

        public async Task<AuthenticationResponse> AuthenticateEmailAsync(string email, string password)
        {
            return await ApiClient.AuthenticateEmailAsync(Options.ServerKey, "", new EmailAuthenticationRequest
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
            return await ApiClient.CreateQRLoginAsync(Options.ServerKey, "", request);
        }

        public Task<ClanDescList> GetClanDescriptionAsync(PaginationParams paginationParams)
        {
            return ApiClient.ListClanDescsAsync(paginationParams);
        }

        IMezonClient IApiClientProvider.MezonApiClient => this;
    }
}
