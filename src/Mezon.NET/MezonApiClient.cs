using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mezon.NET.Abstractions;
using Mezon.NET.Api;
using Mezon.NET.Api.ApiRequests;
using Mezon.NET.Api.ApiResponses;
using Mezon.NET.DependencyInjection.Options;
using Mezon.NET.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mezon.NET
{
    public class MezonApiClient : IMezonApiClient
    {
        private static IDictionary<string, string> _defaultHeaders = new Dictionary<string, string>()
        {
            { "Accept", "application/json" },
        };

        private readonly ILogger<IMezonApiClient> _logger;

        string IMezonApiClient.GatewayBasePath => GatewayBasePath;
        protected string GatewayBasePath { get; private set; }

        string IMezonApiClient.ApiBasePath => ApiBasePath;
        protected string ApiBasePath { get; private set; }

        protected HttpClient HttpClient { get; private set; }

        protected MezonApiClientOptions Options { get; private set; }

        public MezonApiClient(ILogger<IMezonApiClient> logger, HttpClient httpClient, IOptions<MezonApiClientOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Options = options.Value ?? throw new ArgumentNullException(nameof(options));
            HttpClient = httpClient;
            GatewayBasePath = Options.GatewayBasePath;
            HttpClient.Timeout = TimeSpan.FromMilliseconds(Options.TimeoutInMilliseconds);
        }

        public void ConfigureMezonApiBasePath(string apiBasePath)
        {
            Options.ApiBasePath = apiBasePath;
            ApiBasePath = apiBasePath;
        }

        #region private methods

        private async Task<T> SendRequestAsync<T>(
            string urlPath,
            HttpMethod method,
            string? bearerToken = null,
            string? basicAuthUsername = null,
            string? basicAuthPassword = null,
            object? body = null,
            Dictionary<string, object>? queryParams = null,
            CancellationToken cancellationToken = default)
        {
            Check.NotNullOrEmpty(urlPath, nameof(urlPath));
            var fullUri = BuildFullUri(ApiBasePath, urlPath, queryParams);
            _logger.LogDebug("Sending {Method} request to {Url}", method, fullUri);

            using var request = new HttpRequestMessage(method, fullUri);
            //request.BuildHttpHeader(_defaultHeaders);

            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            if (body != null)
            {
                var json = Json.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var timeoutCts = new CancellationTokenSource(Options.TimeoutInMilliseconds);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var response = await HttpClient.SendAsync(request, linkedCts.Token);
                return await HandleHttpResponseAsync<T>(response);
            }
            catch (TaskCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException("Request timed out.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task<T> SendRequestWithBasicAuthAsync<T>(
            string urlPath,
            HttpMethod method,
            string? basicAuthUsername = null,
            string? basicAuthPassword = null,
            object? body = null,
            Dictionary<string, object>? queryParams = null,
            CancellationToken cancellationToken = default)
        {
            Check.NotNullOrEmpty(urlPath, nameof(urlPath));
            var fullUri = BuildFullUri(ApiBasePath, urlPath, queryParams);
            _logger.LogDebug("Sending {Method} request to {Url}", method, fullUri);

            using var request = new HttpRequestMessage(method, fullUri);
            //request.BuildHttpHeader(_defaultHeaders);

            if (!string.IsNullOrEmpty(basicAuthUsername))
            {
                var basicAuthToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{basicAuthUsername}:{basicAuthPassword}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthToken);
            }

            if (body != null)
            {
                var json = Json.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var timeoutCts = new CancellationTokenSource(Options.TimeoutInMilliseconds);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var response = await HttpClient.SendAsync(request, linkedCts.Token);
                return await HandleHttpResponseAsync<T>(response);
            }
            catch (TaskCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException("Request timed out.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task<T> SendGatewayRequestAsync<T>(
            string urlPath,
            HttpMethod method,
            string? basicAuthUsername = null,
            string? basicAuthPassword = null,
            object? body = null,
            Dictionary<string, object>? queryParams = null,
            CancellationToken cancellationToken = default)
        {
            Check.NotNullOrEmpty(urlPath, nameof(urlPath));
            var fullUri = BuildFullUri(GatewayBasePath, urlPath, queryParams);
            _logger.LogDebug("Sending {Method} request to {Url}", method, fullUri);

            using var request = new HttpRequestMessage(method, fullUri);
            //request.BuildHttpHeader(_defaultHeaders);

            if (!string.IsNullOrEmpty(basicAuthUsername))
            {
                var basicAuthToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{basicAuthUsername}:{basicAuthPassword}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthToken);
            }

            if (body != null)
            {
                var json = Json.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var timeoutCts = new CancellationTokenSource(Options.TimeoutInMilliseconds);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var response = await HttpClient.SendAsync(request, linkedCts.Token);
                return await HandleHttpResponseAsync<T>(response);
            }
            catch (TaskCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException("Request timed out.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private Uri BuildFullUri([NotNull] string baseUrl, string relativeUrl, Dictionary<string, object>? queryParams = null)
        {
            Check.NotNullOrEmpty(baseUrl, nameof(baseUrl));
            Check.NotNullOrEmpty(relativeUrl, nameof(relativeUrl));

            var apiUri = new Uri(new Uri(baseUrl), relativeUrl);
            var builder = new UriBuilder(apiUri);
            if (queryParams != null && queryParams.Count > 0)
            {
                var queryString = new StringBuilder();
                foreach (var param in queryParams)
                {
                    if (!string.IsNullOrEmpty(param.Key) && param.Value != null)
                    {
                        if (queryString.Length > 0)
                        {
                            queryString.Append('&');
                        }

                        var key = WebUtility.UrlEncode(param.Key);
                        var value = param.Value != null ? WebUtility.UrlEncode(param.Value.ToString()) : string.Empty;

                        queryString.Append($"{key}={value}");
                    }
                }

                builder.Query = queryString.ToString();
            }

            return builder.Uri;
        }

        private async Task<T> HandleHttpResponseAsync<T>(HttpResponseMessage response)
        {
            _logger.LogDebug("Received response with status code {StatusCode}", response.StatusCode);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return await Task.FromResult<T>(default);
            }
            else if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                return Json.Deserialize<T>(responseString);
            }
            else
            {
                throw new HttpRequestException($"Request failed with status code {response.StatusCode}");
            }
        }
        #endregion

        public Task<object> HealthcheckAsync(string bearerToken)
            => SendRequestAsync<object>("/healthcheck", HttpMethod.Get, bearerToken: bearerToken);

        public Task DeleteAccountAsync(string bearerToken)
            => SendRequestAsync<object>("/v2/account", HttpMethod.Delete, bearerToken: bearerToken);

        public Task<AccountResponse> GetAccountAsync(string bearerToken)
            => SendRequestAsync<AccountResponse>("/v2/account", HttpMethod.Get, bearerToken: bearerToken);

        public Task UpdateAccountAsync(string bearerToken, UpdateAccountRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<object>("/v2/account", HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task<AuthenticationResponse> CheckLoginRequestAsync(string basicAuthUsername, string basicAuthPassword, ApiConfirmLoginRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<AuthenticationResponse>("/v2/account/authenticate/checklogin", HttpMethod.Post, basicAuthUsername: basicAuthUsername, basicAuthPassword: basicAuthPassword, body: body);
        }

        public Task ConfirmLoginAsync(string bearerToken, string basePath, ConfirmLoginRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<object>("/v2/account/authenticate/confirmlogin", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task<LoginIDResponse> CreateQRLoginAsync(string basicAuthUsername, string basicAuthPassword, LoginIDRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<LoginIDResponse>("/v2/account/authenticate/createqrlogin", HttpMethod.Post, basicAuthUsername: basicAuthUsername, basicAuthPassword: basicAuthPassword, body: body);
        }

        public Task<AuthenticationResponse> AuthenticateEmailAsync(string basicAuthUsername, string basicAuthPassword, EmailAuthenticationRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<AuthenticationResponse>("/v2/account/authenticate/email", HttpMethod.Post, basicAuthUsername: basicAuthUsername, basicAuthPassword: basicAuthPassword, body: body);
        }

        public Task<AuthenticationResponse> AuthenticateMezonAsync(string basicAuthUsername, string basicAuthPassword, AccountMezonRequest account, bool? create = null, string username = null, bool? isRemember = null)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            var queryParams = new Dictionary<string, object>
        {
            { "create", create },
            { "username", username },
            { "is_remember", isRemember }
        };
            return SendRequestAsync<AuthenticationResponse>("/v2/account/authenticate/mezon", HttpMethod.Post, basicAuthUsername: basicAuthUsername, basicAuthPassword: basicAuthPassword, body: account, queryParams: queryParams);
        }

        public Task LinkEmailAsync(string bearerToken, AccountEmailRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<object>("/v2/account/link/email", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task LinkMezonAsync(string bearerToken, AccountMezonRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<object>("/v2/account/link/mezon", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task<AuthenticationResponse> RegisterEmailAsync(string bearerToken, RegistrationEmailRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<AuthenticationResponse>("/v2/account/registry", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task<AuthenticationResponse> RefreshSessionAsync(string basicAuthUsername, string basicAuthPassword, SessionRefreshRequest body, CancellationToken cancellationToken = default)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestWithBasicAuthAsync<AuthenticationResponse>("/v2/account/session/refresh", HttpMethod.Post, basicAuthUsername: basicAuthUsername, basicAuthPassword: basicAuthPassword, body: body, cancellationToken: cancellationToken);
        }

        public Task UnlinkEmailAsync(string bearerToken, AccountEmailRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<object>("/v2/account/unlink/email", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task UnlinkMezonAsync(string bearerToken, AccountMezonRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<object>("/v2/account/unlink/mezon", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task<UserActivitiesResponse> GetActivitiesAsync(string bearerToken) =>
            SendRequestAsync<UserActivitiesResponse>("/v2/activity", HttpMethod.Get, bearerToken: bearerToken);

        public Task<UserActivityResponse> CreateActiviyAsync(string bearerToken, CreateActivityRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<UserActivityResponse>("/v2/activity", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task<AppResponse> AddAppAsync(string bearerToken, AddAppRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<AppResponse>("/v2/apps/add", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task<AppsResponse> GetAppsAsync(string bearerToken, string filter = null, bool? tombstones = null, string cursor = null)
        {
            var queryParams = new Dictionary<string, object>
            {
                { "filter", filter },
                { "tombstones", tombstones },
                { "cursor", cursor }
            };
            return SendRequestAsync<AppsResponse>("/v2/apps/app", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        // Add an application to a clan
        public Task AddAppToClanAsync(string bearerToken, string appId, string clanId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                throw new ArgumentNullException(nameof(appId));
            }

            if (string.IsNullOrEmpty(clanId))
            {
                throw new ArgumentNullException(nameof(clanId));
            }

            var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(appId)}/clan/{Uri.EscapeDataString(clanId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken);
        }

        public Task DeleteAppAsync(string bearerToken, string id, bool? recordDeletion = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var queryParams = new Dictionary<string, object>
            {
                { "record_deletion", recordDeletion }
            };
            var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task<AppResponse> GetAppAsync(string bearerToken, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}";
            return SendRequestAsync<AppResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken);
        }

        public Task<AppResponse> UpdateAppAsync(string bearerToken, string id, MezonUpdateAppRequest body)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            Check.NotNull(body, nameof(body));
            var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}";
            return SendRequestAsync<AppResponse>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task BanAppAsync(string bearerToken, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}/ban";
            return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken);
        }

        public Task UnbanAppAsync(string bearerToken, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}/unban";
            return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken);
        }

        public Task<AuditLogsResponse> GetAuditLogsAsync(string bearerToken, string actionLog = null, string userId = null, string clanId = null, string dateLog = null)
        {
            var queryParams = new Dictionary<string, object>
        {
            { "action_log", actionLog },
            { "user_id", userId },
            { "clan_id", clanId },
            { "date_log", dateLog }
        };
            return SendRequestAsync<AuditLogsResponse>("/v2/audit_log", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task UpdateCategoryOrderAsync(string bearerToken, UpdateCategoryOrdersRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<object>("/v2/category/orders", HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task<CategoryDescriptionsResponse> GetCategoryDescriptionsAsync(string bearerToken, string clanId, string creatorId = null, string categoryName = null, string categoryId = null, int? categoryOrder = null)
        {
            if (string.IsNullOrEmpty(clanId))
            {
                throw new ArgumentNullException(nameof(clanId));
            }

            var queryParams = new Dictionary<string, object>
        {
            { "creator_id", creatorId },
            { "category_name", categoryName },
            { "category_id", categoryId },
            { "category_order", categoryOrder }
        };
            var urlPath = $"/v2/categorydesc/{Uri.EscapeDataString(clanId)}";
            return SendRequestAsync<CategoryDescriptionsResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task<AuthenticationResponse> AuthenticateAppAsync(string basicAuthUsername, string basicAuthPassword, AppAuthenticationRequest body, CancellationToken cancellationToken = default)
        {
            Check.NotNull(body, nameof(body));
            return SendGatewayRequestAsync<AuthenticationResponse>("/v2/apps/authenticate/token", HttpMethod.Post, basicAuthUsername: basicAuthUsername, basicAuthPassword: basicAuthPassword, body: body, cancellationToken: cancellationToken);
        }

        public Task<ClanDescriptionsResponse> GetClanDescriptionsAsync(string bearerToken, int? limit = null, int? state = null, string? cusor = null, CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<ClanDescriptionsResponse>("/v2/clandesc", HttpMethod.Get, bearerToken: bearerToken, queryParams: new Dictionary<string, object>
            {
                { "limit", limit },
                { "state", state },
                { "cursor", cusor }
            }, cancellationToken: cancellationToken);
        }

        #region Friends

        public Task AddFriendsAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null)
        {
            var queryParams = new Dictionary<string, object>
            {
                { "ids", ids },
                { "usernames", usernames },
            };
            return SendRequestAsync<object>("/v2/friend", HttpMethod.Post, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task BlockFriendsAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null)
        {
            var queryParams = new Dictionary<string, object>
            {
                { "ids", ids },
                { "usernames", usernames },
            };
            return SendRequestAsync<object>("/v2/friend/block", HttpMethod.Post, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task UnblockFriendsAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null)
        {
            var queryParams = new Dictionary<string, object>
            {
                { "ids", ids },
                { "usernames", usernames },
            };
            return SendRequestAsync<object>("/v2/friend/unblock", HttpMethod.Post, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task DeleteFriendsAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null)
        {
            var queryParams = new Dictionary<string, object>
            {
                { "ids", ids },
                { "usernames", usernames },
            };
            return SendRequestAsync<object>("/v2/friend", HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task<FriendsResponse> GetFriendsAsync(string bearerToken, int? state = null, int? limit = null, string cursor = null)
        {
            var queryParams = new Dictionary<string, object>
            {
                { "state", state },
                { "limit", limit },
                { "cursor", cursor }
            };
            return SendRequestAsync<FriendsResponse>("/v2/friend", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        #endregion

        #region Clan

        public Task<ClanDescriptionResponse> CreateClanDescriptionAsync(string bearerToken, CreateClanDescriptionRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<ClanDescriptionResponse>("/v2/clandesc", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task DeleteClanDescriptionAsync(string bearerToken, string clanId)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            var urlPath = $"/v2/clandesc/{Uri.EscapeDataString(clanId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken);
        }

        public Task UpdateClanDescriptionAsync(string bearerToken, string clanId, object body)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            Check.NotNull(body, nameof(body));
            var urlPath = $"/v2/clandesc/{Uri.EscapeDataString(clanId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task<ClanDescriptionProfileResponse> GetClanDescriptionProfileAsync(string bearerToken, string clanId)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            var urlPath = $"/v2/clandesc/{Uri.EscapeDataString(clanId)}/profile";
            return SendRequestAsync<ClanDescriptionProfileResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken);
        }

        public Task UpdateClanDescriptionProfileAsync(string bearerToken, string clanId, object body)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            Check.NotNull(body, nameof(body));
            var urlPath = $"/v2/clandescprofile/{Uri.EscapeDataString(clanId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task<ClanUsersResponse> GetClanUsersAsync(string bearerToken, string clanId)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            var urlPath = $"/v2/clandesc/{Uri.EscapeDataString(clanId)}/user";
            return SendRequestAsync<ClanUsersResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken);
        }

        public Task KickClanUsersAsync(string bearerToken, string clanId, IEnumerable<string> userIds)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            Check.NotNull(userIds, nameof(userIds));
            var queryParams = new Dictionary<string, object>
            {
                { "user_ids", userIds }
            };
            var urlPath = $"/v2/clandesc/{Uri.EscapeDataString(clanId)}/kick";
            return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task<CheckDuplicateClanNameResponse> CheckDuplicateClanNameAsync(string bearerToken, string clanName)
        {
            Check.NotNullOrEmpty(clanName, nameof(clanName));
            return SendRequestAsync<CheckDuplicateClanNameResponse>($"/v2/clandesc/{Uri.EscapeDataString(clanName)}", HttpMethod.Get, bearerToken: bearerToken);
        }

        #endregion

        #region Channel

        public Task<ChannelDescriptionResponse> CreateChannelDescriptionAsync(string bearerToken, CreateChannelDescriptionRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<ChannelDescriptionResponse>("/v2/channeldesc", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task DeleteChannelDescAsync(string bearerToken, string channelId)
        {
            Check.NotNullOrEmpty(channelId, nameof(channelId));
            var urlPath = $"/v2/channeldesc/{Uri.EscapeDataString(channelId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken);
        }

        public Task UpdateChannelDescriptionAsync(string bearerToken, string channelId, object body)
        {
            Check.NotNullOrEmpty(channelId, nameof(channelId));
            Check.NotNull(body, nameof(body));
            var urlPath = $"/v2/channeldesc/{Uri.EscapeDataString(channelId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task AddChannelUsersAsync(string bearerToken, string channelId, IEnumerable<string> userIds)
        {
            Check.NotNullOrEmpty(channelId, nameof(channelId));
            Check.NotNull(userIds, nameof(userIds));
            var queryParams = new Dictionary<string, object>
            {
                { "user_ids", userIds }
            };
            var urlPath = $"/v2/channel/{Uri.EscapeDataString(channelId)}/add";
            return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task RemoveChannelUsersAsync(string bearerToken, string channelId, IEnumerable<string> userIds)
        {
            Check.NotNullOrEmpty(channelId, nameof(channelId));
            Check.NotNull(userIds, nameof(userIds));
            var queryParams = new Dictionary<string, object>
            {
                { "user_ids", userIds }
            };
            var urlPath = $"/v2/channel/{Uri.EscapeDataString(channelId)}/remove";
            return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task<ChannelMessagesResponse> GetChannelMessagesAsync(string bearerToken, string clanId, string channelId, string messageId, int? direction = null, int? limit = null, string? topicId = null)
        {
            Check.NotNullOrEmpty(channelId, nameof(channelId));
            var queryParams = new Dictionary<string, object>
            {
                { "clan_id", clanId },
                { "message_id", messageId },
                { "limit", limit },
                { "direction", direction },
                { "topic_id", topicId }
            };
            var urlPath = $"/v2/channel/{Uri.EscapeDataString(channelId)}";
            return SendRequestAsync<ChannelMessagesResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task<ChannelUsersResponse> GetChannelUsersAsync(string bearerToken, string clanId, string channelId, string channelType, int? limit = null, int? state = null, string cursor = null)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            Check.NotNullOrEmpty(channelId, nameof(channelId));
            var queryParams = new Dictionary<string, object>
            {
                { "clan_id", clanId },
                { "channel_type", channelType },
                { "limit", limit },
                { "state", state },
                { "cursor", cursor }
            };
            var urlPath = $"/v2/channel/{Uri.EscapeDataString(channelId)}/user";
            return SendRequestAsync<ChannelUsersResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        #endregion

        #region User

        public Task<UsersResponse> GetUsersAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null)
        {
            var queryParams = new Dictionary<string, object>();
            if (ids != null)
            {
                queryParams["ids"] = ids;
            }

            if (usernames != null)
            {
                queryParams["usernames"] = usernames;
            }

            return SendRequestAsync<UsersResponse>("/v2/user", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task UpdateUserStatusAsync(string bearerToken, UpdateUserStatusRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<object>("/v2/userstatus", HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task<UserStatusResponse> GetUserStatusAsync(string bearerToken)
        {
            return SendRequestAsync<UserStatusResponse>("/v2/userstatus", HttpMethod.Get, bearerToken: bearerToken);
        }

        #endregion

        #region Roles

        public Task<RoleResponse> CreateRoleAsync(string bearerToken, CreateRoleRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<RoleResponse>("/v2/roles", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task DeleteRoleAsync(string bearerToken, string roleId, string? channelId = null, string? clanId = null, string? roleLabel = null)
        {
            Check.NotNullOrEmpty(roleId, nameof(roleId));
            var urlPath = $"/v2/roles/{Uri.EscapeDataString(roleId)}";
            var queryParams = new Dictionary<string, object>
            {
                { "channel_id", channelId },
                { "clan_id", clanId },
                { "role_label", roleLabel }
            };
            return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken);
        }

        public Task UpdateRoleAsync(string bearerToken, string roleId, UpdateRoleRequest body)
        {
            Check.NotNullOrEmpty(roleId, nameof(roleId));
            Check.NotNull(body, nameof(body));
            var urlPath = $"/v2/roles/{Uri.EscapeDataString(roleId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task<RoleEventResponse> GetRolesAsync(string bearerToken, string? clanId = null, int? limit = null, int? state = null, string cursor = null)
        {
            var urlPath = "/v2/roles";
            var queryParams = new Dictionary<string, object>
            {
                { "clan_id", clanId },
                { "limit", limit },
                { "state", state },
                { "cursor", cursor }
            };
            return SendRequestAsync<RoleEventResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        #endregion

        #region Notifications

        public Task DeleteNotificationsAsync(string bearerToken, IEnumerable<string>? ids = null, string? category = null)
        {
            var queryParams = new Dictionary<string, object>();
            if (ids != null)
            {
                queryParams["ids"] = ids;
            }

            if (category != null)
            {
                queryParams["category"] = category;
            }

            return SendRequestAsync<object>("/v2/notification", HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task<NotificationsResponse> GetNotificationsAsync(string bearerToken, string? clanId = null, string? notificationId = null, string? category = null, int? limit = null, int? direction = null)
        {
            var queryParams = new Dictionary<string, object>();
            if (clanId != null)
            {
                queryParams["clan_id"] = clanId;
            }

            if (notificationId != null)
            {
                queryParams["notification_id"] = notificationId;
            }

            if (category != null)
            {
                queryParams["category"] = category;
            }

            if (limit != null)
            {
                queryParams["limit"] = limit;
            }

            if (direction != null)
            {
                queryParams["direction"] = direction;
            }

            return SendRequestAsync<NotificationsResponse>("/v2/notification", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        #endregion

        #region Storage

        public Task<UploadAttachmentResponse> UploadAttachmentFileAsync(string bearerToken, UploadAttachmentRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<UploadAttachmentResponse>("/v2/uploadattachmentfile", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        #endregion

        #region Category
        public Task<CategoryDescriptionResponse> CreateCategoryDescriptionAsync(string bearerToken, CreateCategoryDescriptionRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<CategoryDescriptionResponse>("/v2/createcategory", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task DeleteCategoryDescriptionAsync(string bearerToken, string categoryId, string clanId, string? categoryLabel = null)
        {
            Check.NotNullOrEmpty(categoryId, nameof(categoryId));
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            var queryParams = new Dictionary<string, object>();
            if (categoryLabel != null)
            {
                queryParams["category_label"] = categoryLabel;
            }

            var urlPath = $"/v2/deletecategory/category_id/{Uri.EscapeDataString(categoryId)}/clan_id/{Uri.EscapeDataString(clanId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task UpdateCategoryAsync(string bearerToken, string clanId, UpdateCategoryRequest body)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            Check.NotNull(body, nameof(body));
            var urlPath = $"/v2/categorydesc/{Uri.EscapeDataString(clanId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        }
        #endregion

        #region Events
        public Task<EventManagementResponse> CreateEventAsync(string bearerToken, CreateEventRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<EventManagementResponse>("/v2/eventmanagement/create", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task DeleteEventAsync(string bearerToken, string eventId, string clanId, string creatorId, string eventLabel = null, string channelId = null)
        {
            Check.NotNullOrEmpty(eventId, nameof(eventId));
            var queryParams = new Dictionary<string, object>
            {
                { "clan_id", clanId }, { "creator_id", creatorId }, { "event_label", eventLabel }, { "channel_id", channelId }
            };
            var urlPath = $"/v2/event/{Uri.EscapeDataString(eventId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task UpdateEventUserAsync(string bearerToken, UpdateEventUserRequest body)
        {
            Check.NotNull(body, nameof(body));
            var urlPath = "/v2/eventmanagement/user";
            return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task UpdateEventAsync(string bearerToken, string eventId, UpdateEventRequest body)
        {
            Check.NotNullOrEmpty(eventId, nameof(eventId));
            Check.NotNull(body, nameof(body));
            var urlPath = $"/v2/eventmanagement/{Uri.EscapeDataString(eventId)}";
            return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        }

        public Task<EventManagementsResponse> GetEventsAsync(string bearerToken, string? clanId = null)
        {
            var queryParams = new Dictionary<string, object>();
            if (clanId != null)
            {
                queryParams["clan_id"] = clanId;
            }

            return SendRequestAsync<EventManagementsResponse>("/v2/eventmanagement", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task AddUserEventAsync(string bearerToken, AddUserEventRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<object>("/v2/userevent", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task DeleteUserEventAsync(string bearerToken, string clanId, string eventId)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            Check.NotNullOrEmpty(eventId, nameof(eventId));
            var queryParams = new Dictionary<string, object>
            {
                { "clan_id", clanId },
                { "event_id", eventId }
            };
            return SendRequestAsync<object>("/v2/userevent", HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        }
        #endregion

        #region Permissions
        public Task<PermissionsResponse> GetPermissionsAsync(string bearerToken) =>
            SendRequestAsync<PermissionsResponse>("/v2/permissions", HttpMethod.Get, bearerToken: bearerToken);

        public Task<PermissionsResponse> GetRolePermissionsAsync(string bearerToken, string roleId)
        {
            Check.NotNullOrEmpty(roleId, nameof(roleId));
            var urlPath = $"/v2/roles/{Uri.EscapeDataString(roleId)}/permissions";
            return SendRequestAsync<PermissionsResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken);
        }

        public Task<RoleUsersResponse> ListRoleUsersAsync(string bearerToken, string roleId, int? limit = null, string cursor = null)
        {
            Check.NotNullOrEmpty(roleId, nameof(roleId));
            var queryParams = new Dictionary<string, object>();
            if (limit != null)
            {
                queryParams["limit"] = limit;
            }

            if (cursor != null)
            {
                queryParams["cursor"] = cursor;
            }

            var urlPath = $"/v2/roles/{Uri.EscapeDataString(roleId)}/users";
            return SendRequestAsync<RoleUsersResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task<UserPermissionsInChannelResponse> GetUserPermissionsInChannelAsync(string bearerToken, string clanId, string channelId)
        {
            var queryParams = new Dictionary<string, object>
            {
                { "clan_id", clanId },
                { "channel_id", channelId }
            };
            return SendRequestAsync<UserPermissionsInChannelResponse>("/v2/users/clans/channels", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }
        #endregion

        #region Invites
        public Task<LinkInviteUserResponse> CreateLinkInviteUserAsync(string bearerToken, LinkInviteUserRequest body)
        {
            Check.NotNull(body, nameof(body));
            return SendRequestAsync<LinkInviteUserResponse>("/v2/invite", HttpMethod.Post, bearerToken: bearerToken, body: body);
        }

        public Task<InviteUserResponse> GetLinkInviteAsync(string basicAuthUsername, string basicAuthPassword, string inviteId)
        {
            Check.NotNullOrEmpty(inviteId, nameof(inviteId));
            var urlPath = $"/v2/invite/{Uri.EscapeDataString(inviteId)}";
            return SendRequestWithBasicAuthAsync<InviteUserResponse>(urlPath, HttpMethod.Get, basicAuthUsername: basicAuthUsername, basicAuthPassword: basicAuthPassword);
        }

        public Task<InviteUserResponse> InviteUserAsync(string bearerToken, string inviteId)
        {
            Check.NotNullOrEmpty(inviteId, nameof(inviteId));
            var urlPath = $"/v2/invite/{Uri.EscapeDataString(inviteId)}";
            return SendRequestAsync<InviteUserResponse>(urlPath, HttpMethod.Post, bearerToken: bearerToken);
        }
        #endregion

        #region Notification Settings
        public Task SetNotificationClanSettingAsync(string bearerToken, SetDefaultNotificationRequest body) =>
            SendRequestAsync<object>("/v2/notificationclan/set", HttpMethod.Post, bearerToken: bearerToken, body: body);

        public Task SetNotificationChannelSettingAsync(string bearerToken, SetNotificationChannelRequest body) =>
            SendRequestAsync<object>("/v2/notificationchannel/set", HttpMethod.Post, bearerToken: bearerToken, body: body);

        public Task SetMuteNotificationCategoryAsync(string bearerToken, SetMuteNotificationRequest body) =>
            SendRequestAsync<object>("/v2/mutenotificationcategory/set", HttpMethod.Post, bearerToken: bearerToken, body: body);

        public Task SetMuteNotificationChannelAsync(string bearerToken, SetMuteNotificationRequest body) =>
            SendRequestAsync<object>("/v2/mutenotificationchannel/set", HttpMethod.Post, bearerToken: bearerToken, body: body);

        public Task<NotificationChannelCategorySettingsResponse> GetChannelCategoryNotificationSettingsAsync(string bearerToken, string clanId)
        {
            var queryParams = new Dictionary<string, object> { { "clan_id", clanId } };
            return SendRequestAsync<NotificationChannelCategorySettingsResponse>("/v2/getnotificationchannel", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }

        public Task<ClanNotificationSettingResponse> GetClanNotificationSettingAsync(string bearerToken, string clanId)
        {
            Check.NotNullOrEmpty(clanId, nameof(clanId));
            var urlPath = "/v2/getnotificationclan";
            var queryParams = new Dictionary<string, object> { { "clan_id", clanId } };
            return SendRequestAsync<ClanNotificationSettingResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        }
        #endregion

        #region Messages (Advanced)
        //public Task<SearchMessageResponse> SearchMessageAsync(string bearerToken, SearchMessageRequest body) =>
        //    SendRequestAsync<SearchMessageResponse>("/v2/message/search", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task<ChannelMessageHeaderResponse> CreatePinMessageAsync(string bearerToken, PinMessageRequest body) =>
        //    SendRequestAsync<ChannelMessageHeaderResponse>("/v2/message/pin", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task<PinMessagesListResponse> GetPinMessagesListAsync(string bearerToken, string channelId, string clanId)
        //{
        //    var queryParams = new Dictionary<string, object> { { "channel_id", channelId }, { "clan_id", clanId } };
        //    return SendRequestAsync<PinMessagesListResponse>("/v2/message/pin", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task DeletePinMessageAsync(string bearerToken, string messageId, string channelId, string clanId)
        //{
        //    var queryParams = new Dictionary<string, object> { { "message_id", messageId }, { "channel_id", channelId }, { "clan_id", clanId } };
        //    return SendRequestAsync<object>("/v2/message/pin", HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task MarkAsReadAsync(string bearerToken, MarkAsReadRequest body) =>
        //    SendRequestAsync<object>("/v2/message/read", HttpMethod.Post, bearerToken: bearerToken, body: body);
        #endregion

        //#region Emoji & Stickers
        //public Task CreateClanEmojiAsync(string bearerToken, ClanEmojiCreateRequest body) =>
        //    SendRequestAsync<object>("/v2/emoji", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task UpdateClanEmojiByIdAsync(string bearerToken, string emojiId, UpdateClanEmojiRequest body)
        //{
        //    Check.NotNullOrEmpty(emojiId, nameof(emojiId));
        //    var urlPath = $"/v2/emoji/{Uri.EscapeDataString(emojiId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task DeleteClanEmojiByIdAsync(string bearerToken, string emojiId, string clanId)
        //{
        //    Check.NotNullOrEmpty(emojiId, nameof(emojiId));
        //    var queryParams = new Dictionary<string, object> { { "clan_id", clanId } };
        //    var urlPath = $"/v2/emoji/{Uri.EscapeDataString(emojiId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task AddClanStickerAsync(string bearerToken, ClanStickerAddRequest body) =>
        //    SendRequestAsync<object>("/v2/sticker", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task UpdateClanStickerByIdAsync(string bearerToken, string stickerId, UpdateClanStickerRequest body)
        //{
        //    Check.NotNullOrEmpty(stickerId, nameof(stickerId));
        //    var urlPath = $"/v2/sticker/{Uri.EscapeDataString(stickerId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task DeleteClanStickerByIdAsync(string bearerToken, string stickerId, string clanId)
        //{
        //    Check.NotNullOrEmpty(stickerId, nameof(stickerId));
        //    var queryParams = new Dictionary<string, object> { { "clan_id", clanId } };
        //    var urlPath = $"/v2/sticker/{Uri.EscapeDataString(stickerId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task<EmojiListedResponse> GetListEmojisByUserIdAsync(string bearerToken) =>
        //    SendRequestAsync<EmojiListedResponse>("/v2/emoji/user", HttpMethod.Get, bearerToken: bearerToken);

        //public Task<StickerListedResponse> GetListStickersByUserIdAsync(string bearerToken) =>
        //    SendRequestAsync<StickerListedResponse>("/v2/sticker/user", HttpMethod.Get, bearerToken: bearerToken);
        //#endregion

        //#region Webhooks
        //public Task<WebhookGenerateResponse> GenerateWebhookAsync(string bearerToken, WebhookCreateRequest body) =>
        //    SendRequestAsync<WebhookGenerateResponse>("/v2/webhook", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task<WebhookListResponse> ListWebhookByChannelIdAsync(string bearerToken, string channelId, string clanId)
        //{
        //    var queryParams = new Dictionary<string, object> { { "channel_id", channelId }, { "clan_id", clanId } };
        //    return SendRequestAsync<WebhookListResponse>("/v2/webhook", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task UpdateWebhookByIdAsync(string bearerToken, string webhookId, UpdateWebhookRequest body)
        //{
        //    Check.NotNullOrEmpty(webhookId, nameof(webhookId));
        //    var urlPath = $"/v2/webhook/{Uri.EscapeDataString(webhookId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task DeleteWebhookByIdAsync(string bearerToken, string webhookId, DeleteWebhookRequest body)
        //{
        //    Check.NotNullOrEmpty(webhookId, nameof(webhookId));
        //    // The body suggests it's not a standard DELETE, but a POST/PUT for soft-delete. Assuming PUT.
        //    var urlPath = $"/v2/webhook/{Uri.EscapeDataString(webhookId)}/disable";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}
        //#endregion

        //#region System Messages
        //public Task<SystemMessagesListResponse> GetSystemMessagesListAsync(string bearerToken) =>
        //    SendRequestAsync<SystemMessagesListResponse>("/v2/system-message", HttpMethod.Get, bearerToken: bearerToken);

        //public Task<SystemMessageResponse> GetSystemMessageByClanIdAsync(string bearerToken, string clanId)
        //{
        //    Check.NotNullOrEmpty(clanId, nameof(clanId));
        //    var urlPath = $"/v2/system-message/{Uri.EscapeDataString(clanId)}";
        //    return SendRequestAsync<SystemMessageResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken);
        //}

        //public Task CreateSystemMessageAsync(string bearerToken, SystemMessageRequest body) =>
        //    SendRequestAsync<object>("/v2/system-message", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task UpdateSystemMessageAsync(string bearerToken, string clanId, UpdateSystemMessageRequest body)
        //{
        //    Check.NotNullOrEmpty(clanId, nameof(clanId));
        //    var urlPath = $"/v2/system-message/{Uri.EscapeDataString(clanId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task DeleteSystemMessageAsync(string bearerToken, string clanId)
        //{
        //    Check.NotNullOrEmpty(clanId, nameof(clanId));
        //    var urlPath = $"/v2/system-message/{Uri.EscapeDataString(clanId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken);
        //}
        //#endregion

        //#region Ordering
        //public Task UpdateRoleOrderAsync(string bearerToken, UpdateRoleOrderRequest body) =>
        //    SendRequestAsync<object>("/v2/role/orders", HttpMethod.Put, bearerToken: bearerToken, body: body);

        //public Task UpdateClanOrderAsync(string bearerToken, UpdateClanOrderRequest body) =>
        //    SendRequestAsync<object>("/v2/clan/orders", HttpMethod.Put, bearerToken: bearerToken, body: body);
        //#endregion

        //#region Encryption
        //public Task<ChanEncryptionMethodResponse> GetChanEncryptionMethodAsync(string bearerToken, string channelId)
        //{
        //    Check.NotNullOrEmpty(channelId, nameof(channelId));
        //    var urlPath = $"/v2/encryption/channel/{Uri.EscapeDataString(channelId)}";
        //    return SendRequestAsync<ChanEncryptionMethodResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken);
        //}

        //public Task SetChanEncryptionMethodAsync(string bearerToken, string channelId, SetChanEncryptionMethodRequest body)
        //{
        //    Check.NotNullOrEmpty(channelId, nameof(channelId));
        //    var urlPath = $"/v2/encryption/channel/{Uri.EscapeDataString(channelId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task<GetPubKeysResponse> GetPublicKeysAsync(string bearerToken, IEnumerable<string> userIds)
        //{
        //    var queryParams = new Dictionary<string, object> { { "user_ids", userIds } };
        //    return SendRequestAsync<GetPubKeysResponse>("/v2/encryption/pubkeys", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task PushPublicKeyAsync(string bearerToken, PushPublicKeyRequest body) =>
        //    SendRequestAsync<object>("/v2/encryption/pubkey", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task<GetKeyServerResponse> GetKeyServerAsync(string bearerToken) =>
        //    SendRequestAsync<GetKeyServerResponse>("/v2/encryption/keyserver", HttpMethod.Get, bearerToken: bearerToken);
        //#endregion

        //#region Onboarding
        //public Task<ListOnboardingResponse> ListOnboardingAsync(string bearerToken, string clanId, int? guideType = null)
        //{
        //    var queryParams = new Dictionary<string, object> { { "clan_id", clanId }, { "guide_type", guideType } };
        //    return SendRequestAsync<ListOnboardingResponse>("/v2/onboarding", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task<OnboardingItemResponse> GetOnboardingDetailAsync(string bearerToken, string id, string clanId)
        //{
        //    Check.NotNullOrEmpty(id, nameof(id));
        //    var queryParams = new Dictionary<string, object> { { "clan_id", clanId } };
        //    var urlPath = $"/v2/onboarding/{Uri.EscapeDataString(id)}";
        //    return SendRequestAsync<OnboardingItemResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task CreateOnboardingAsync(string bearerToken, CreateOnboardingRequest body) =>
        //    SendRequestAsync<object>("/v2/onboarding", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task UpdateOnboardingAsync(string bearerToken, string id, UpdateOnboardingRequest body)
        //{
        //    Check.NotNullOrEmpty(id, nameof(id));
        //    var urlPath = $"/v2/onboarding/{Uri.EscapeDataString(id)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task DeleteOnboardingAsync(string bearerToken, string id, string clanId)
        //{
        //    Check.NotNullOrEmpty(id, nameof(id));
        //    var queryParams = new Dictionary<string, object> { { "clan_id", clanId } };
        //    var urlPath = $"/v2/onboarding/{Uri.EscapeDataString(id)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        //}
        //#endregion

        //#region Wallet & Transactions
        //public Task GiveCoffeeAsync(string bearerToken, GiveCoffeeRequest body) =>
        //    SendRequestAsync<object>("/v2/wallet/givecoffee", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task SendTokenAsync(string bearerToken, TokenSentRequest body) =>
        //    SendRequestAsync<object>("/v2/wallet/sendtoken", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task<TransactionDetailResponse> ListTransactionDetailAsync(string bearerToken, string transId)
        //{
        //    Check.NotNullOrEmpty(transId, nameof(transId));
        //    var urlPath = $"/v2/wallet/transaction/{Uri.EscapeDataString(transId)}";
        //    return SendRequestAsync<TransactionDetailResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken);
        //}

        //public Task<WalletLedgerListResponse> ListWalletLedgerAsync(string bearerToken, int? limit = null, int? filter = null, int? page = null)
        //{
        //    var queryParams = new Dictionary<string, object> { { "limit", limit }, { "filter", filter }, { "page", page } };
        //    return SendRequestAsync<WalletLedgerListResponse>("/v2/wallet/ledger", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}
        //#endregion

        //#region Mezon Meet
        //public Task<GenerateMeetTokenResponse> GenerateMeetTokenAsync(string bearerToken, GenerateMeetTokenRequest body) =>
        //    SendRequestAsync<GenerateMeetTokenResponse>("/v2/meet/token", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task<GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(string bearerToken) =>
        //    SendRequestAsync<GenerateMezonMeetResponse>("/v2/meet/external", HttpMethod.Post, bearerToken: bearerToken);

        //public Task<GenerateMeetTokenExternalResponse> GenerateMeetTokenExternalAsync(string basePath, string token, string displayName, bool? isGuest)
        //{
        //    var queryParams = new Dictionary<string, object> { { "token", token }, { "display_name", displayName }, { "is_guest", isGuest } };
        //    // This is likely a gateway request without authentication
        //    return SendGatewayRequestAsync<GenerateMeetTokenExternalResponse>("/v2/meet/token/external", HttpMethod.Get, queryParams: queryParams);
        //}
        //#endregion

        //#region Ownership
        //public Task TransferOwnershipAsync(string bearerToken, TransferOwnershipRequest body) =>
        //    SendRequestAsync<object>("/v2/clan/transfer-ownership", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //#endregion
    }
}
