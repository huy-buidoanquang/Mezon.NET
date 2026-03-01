using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Mezon.NET.Abstractions;
using Mezon.NET.Core;
using Mezon.NET.Queue;
using Mezon.NET.Utils;
using Mezon.Protobuf.Api;
using Newtonsoft.Json;
using PbSession = Mezon.Protobuf.Api.Session;
using PbRealtime = Mezon.Protobuf.Realtime;

namespace Mezon.NET.Api
{
    internal class MezonApiClient : IMezonApiClient, IDisposable, IAsyncDisposable
    {
        private static readonly ConcurrentDictionary<string, Func<BucketIds, BucketId>> _bucketIdGenerators = new ConcurrentDictionary<string, Func<BucketIds, BucketId>>();

        public event Func<string, string, double, Task> ApiSentRequestEvent { add { _apiSentRequestEvent.Add(value); } remove { _apiSentRequestEvent.Remove(value); } }
        private readonly AsyncEvent<Func<string, string, double, Task>> _apiSentRequestEvent = new AsyncEvent<Func<string, string, double, Task>>();

        protected bool _isDisposed;
        protected readonly JsonSerializer _serializer;
        protected readonly SemaphoreSlim _stateLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _loginCancelToken = new CancellationTokenSource();

        private readonly RestClientProvider _httpClientProvider;
        private readonly GRPCClientProvider _grpcClientProvider;

        internal MezonRequestQueue RequestQueue { get; }
        MezonRequestQueue IMezonApiClient.RequestQueue => RequestQueue;

        public LoginState LoginState { get; private set; }

        internal TokenType TokenType { get; private set; }
        TokenType IMezonApiClient.TokenType => TokenType;

        internal string AuthToken { get; private set; } = string.Empty;
        string IMezonApiClient.AuthToken => AuthToken;

        internal long? CurrentUserId { get; set; }

        long? IMezonApiClient.CurrentUserId => CurrentUserId;

        protected IRestClient RestClient { get; private set; }

        protected IGRPCClient GRPCClient { get; private set; }

        internal bool UseSystemClock { get; set; }

        public RetryMode DefaultRetryMode { get; }

        internal Func<IRateLimitInfo, Task>? DefaultRatelimitCallback { get; set; }

        protected MezonConfiguration MezonConfiguration;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public MezonApiClient(
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
            RestClientProvider restClientProvider,
            GRPCClientProvider grpcClientProvider,
            MezonConfiguration configuration,
            JsonSerializer? serializer = null,
            Func<IRateLimitInfo, Task>? defaultRatelimitCallback = null)
        {
            _httpClientProvider = restClientProvider;
            _grpcClientProvider = grpcClientProvider;
            _serializer = serializer ?? Json.Serializer;
            MezonConfiguration = configuration;
            DefaultRatelimitCallback = defaultRatelimitCallback;
            RequestQueue = new MezonRequestQueue();
            ConfigureGatewayBasePath(configuration.GatewayBasePath);
        }

        /// <exception cref="ArgumentException">Unknown OAuth token type.</exception>
        public virtual void ConfigureGatewayBasePath(string gatewayBasePath)
        {
            RestClient?.Dispose();
            RestClient = _httpClientProvider(gatewayBasePath);
            RestClient.SetHeader("Accept", "*/*");
        }

        /// <exception cref="ArgumentException">Unknown OAuth token type.</exception>
        public virtual void ConfigureApiBasePath(string apiBasePath)
        {
            GRPCClient?.Dispose();
            GRPCClient = _grpcClientProvider(apiBasePath);
        }

        internal static string GetPrefixedToken(TokenType tokenType, string token)
        {
            return tokenType switch
            {
                TokenType.Bot => $"Bot {token}",
                TokenType.Bearer => $"Bearer {token}",
                _ => throw new ArgumentException(message: "Unknown OAuth token type.", paramName: nameof(tokenType)),
            };
        }

        internal virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _loginCancelToken?.Dispose();
                    RestClient?.Dispose();
                    GRPCClient?.Dispose();
                    RequestQueue?.Dispose();
                    _stateLock?.Dispose();
                }
                _isDisposed = true;
            }
        }

        internal virtual async ValueTask DisposeAsync(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _loginCancelToken?.Dispose();
                    RestClient?.Dispose();
                    GRPCClient?.Dispose();

                    if (!(RequestQueue is null))
                    {
                        await RequestQueue.DisposeAsync().ConfigureAwait(false);
                    }

                    _stateLock?.Dispose();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() => Dispose(true);

        public ValueTask DisposeAsync() => DisposeAsync(true);

        public async Task LoginAsync(TokenType tokenType, string token, RequestOptions? options = null)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LoginInternalAsync(tokenType, token, options).ConfigureAwait(false);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        private async Task LoginInternalAsync(TokenType tokenType, string token, RequestOptions? options = null)
        {
            if (LoginState != LoginState.LoggedOut)
            {
                await LogoutInternalAsync().ConfigureAwait(false);
            }

            LoginState = LoginState.LoggingIn;

            try
            {
                _loginCancelToken?.Dispose();
                _loginCancelToken = new CancellationTokenSource();

                await RequestQueue.SetCancelTokenAsync(_loginCancelToken.Token).ConfigureAwait(false);
                RestClient.SetCancelToken(_loginCancelToken.Token);
                GRPCClient.SetCancelToken(_loginCancelToken.Token);

                TokenType = tokenType;
                AuthToken = token.TrimEnd();
                if (tokenType != TokenType.Webhook)
                {
                    RestClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
                }

                LoginState = LoginState.LoggedIn;
            }
            catch
            {
                await LogoutInternalAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task LogoutAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LogoutInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        private async Task LogoutInternalAsync()
        {
            //An exception here will lock the client into the unusable LoggingOut state, but that's probably fine since our client is in an undefined state too.
            if (LoginState == LoginState.LoggedOut)
            {
                return;
            }

            LoginState = LoginState.LoggingOut;

            try
            {
                _loginCancelToken?.Cancel(false);
            }
            catch { }

            await DisconnectInternalAsync(null).ConfigureAwait(false);
            await RequestQueue.ClearAsync().ConfigureAwait(false);

            await RequestQueue.SetCancelTokenAsync(CancellationToken.None).ConfigureAwait(false);
            RestClient.SetCancelToken(CancellationToken.None);
            GRPCClient.SetCancelToken(CancellationToken.None);

            CurrentUserId = null;
            LoginState = LoginState.LoggedOut;
        }

        internal virtual Task ConnectInternalAsync() => Task.CompletedTask;

        internal virtual Task DisconnectInternalAsync(Exception? ex = null) => Task.CompletedTask;

        #region Core
        internal Task SendNoResAsync(string method, Expression<Func<string>> endpointExpr, BucketIds ids, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null, [CallerMemberName] string? funcName = null)
            => SendNoResAsync(method, GetEndpoint(endpointExpr), GetBucketId(method, ids, endpointExpr, funcName ?? string.Empty), clientBucket, options);

        public Task SendNoResAsync(string method, string endpoint, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null)
        {
            options ??= new RequestOptions();
            options.HeaderOnly = true;
            options.BucketId = bucketId;

            var request = new ApiRequest(RestClient, method, endpoint, options);
            return SendInternalAsync(method, endpoint, request);
        }

        internal Task SendJsonNoResAsync(string method, Expression<Func<string>> endpointExpr, object payload, BucketIds ids, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null, [CallerMemberName] string? funcName = null)
            => SendJsonNoResAsync(method, GetEndpoint(endpointExpr), payload, GetBucketId(method, ids, endpointExpr, funcName ?? string.Empty), clientBucket, options);

        public Task SendJsonNoResAsync(string method, string endpoint, object payload, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null)
        {
            options ??= new RequestOptions();
            options.HeaderOnly = true;
            options.BucketId = bucketId;

            string json = payload != null ? SerializeJson(payload) : string.Empty;
            var request = new JsonApiRequest(RestClient, method, endpoint, json, options);
            return SendInternalAsync(method, endpoint, request);
        }

        internal Task SendMultipartNoResAsync(string method, Expression<Func<string>> endpointExpr, IReadOnlyDictionary<string, object> multipartArgs, BucketIds ids, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null, [CallerMemberName] string? funcName = null)
            => SendMultipartNoResAsync(method, GetEndpoint(endpointExpr), multipartArgs, GetBucketId(method, ids, endpointExpr, funcName ?? string.Empty), clientBucket, options);

        public Task SendMultipartNoResAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null)
        {
            options ??= new RequestOptions();
            options.HeaderOnly = true;
            options.BucketId = bucketId;

            var request = new MultipartApiRequest(RestClient, method, endpoint, multipartArgs, options);
            return SendInternalAsync(method, endpoint, request);
        }

        internal Task<Stream> SendAsync(string method, Expression<Func<string>> endpointExpr, BucketIds ids, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null, [CallerMemberName] string? funcName = null)
            => SendAsync(method, GetEndpoint(endpointExpr), GetBucketId(method, ids, endpointExpr, funcName ?? string.Empty), clientBucket, options);

        public async Task<Stream> SendAsync(string method, string endpoint, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null)
        {
            options ??= new RequestOptions();
            options.BucketId = bucketId;

            var request = new ApiRequest(RestClient, method, endpoint, options);
            return await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
        }

        internal Task<Stream> SendJsonAsync(string method, Expression<Func<string>> endpointExpr, object payload, BucketIds ids, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null, [CallerMemberName] string? funcName = null)
            => SendJsonAsync(method, GetEndpoint(endpointExpr), payload, GetBucketId(method, ids, endpointExpr, funcName ?? string.Empty), clientBucket, options);

        public async Task<Stream> SendJsonAsync(string method, string endpoint, object payload, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null)
        {
            options ??= new RequestOptions();
            options.BucketId = bucketId;

            string json = payload != null ? SerializeJson(payload) : string.Empty;

            var request = new JsonApiRequest(RestClient, method, endpoint, json, options);
            return await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
        }

        internal Task<Stream> SendMultipartAsync(string method, Expression<Func<string>> endpointExpr, IReadOnlyDictionary<string, object> multipartArgs, BucketIds ids, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null, [CallerMemberName] string? funcName = null)
            => SendMultipartAsync(method, GetEndpoint(endpointExpr), multipartArgs, GetBucketId(method, ids, endpointExpr, funcName ?? string.Empty), clientBucket, options);

        public async Task<Stream> SendMultipartAsync(string method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null)
        {
            options ??= new RequestOptions();
            options.BucketId = bucketId;

            var request = new MultipartApiRequest(RestClient, method, endpoint, multipartArgs, options);
            return await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
        }

        private async Task<Stream> SendInternalAsync(string method, string endpoint, ApiRequest request)
        {
            if (!request.Options.IgnoreState)
            {
                CheckState();
            }

            request.Options.RetryMode ??= DefaultRetryMode;
            request.Options.UseSystemClock ??= UseSystemClock;
            request.Options.RatelimitCallback ??= DefaultRatelimitCallback;

            var stopwatch = Stopwatch.StartNew();
            var responseStream = await RequestQueue.SendAsync(request).ConfigureAwait(false);
            stopwatch.Stop();

            double milliseconds = ToMilliseconds(stopwatch);
            await _apiSentRequestEvent.InvokeAsync(method, endpoint, milliseconds).ConfigureAwait(false);

            return responseStream;
        }

        private static string GetEndpoint(Expression<Func<string>> endpointExpr)
        {
            return endpointExpr.Compile()();
        }

        private static BucketId GetBucketId(string httpMethod, BucketIds ids, Expression<Func<string>> endpointExpr, string callingMethod)
        {
            ids.HttpMethod ??= httpMethod;
            return _bucketIdGenerators.GetOrAdd(callingMethod, x => CreateBucketId(endpointExpr))(ids);
        }

#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        private static Func<BucketIds, BucketId> CreateBucketId(Expression<Func<string>> endpoint)
        {
            try
            {
                //Is this a constant string
                if (endpoint.Body.NodeType == ExpressionType.Constant)
                {

                    return (x) => BucketId.Create(x.HttpMethod, (endpoint.Body as ConstantExpression).Value.ToString(), x.ToMajorParametersDictionary());

                }

                var builder = new StringBuilder();
                var methodCall = endpoint.Body as MethodCallExpression;
                Expression[] methodArgs = methodCall.Arguments.ToArray() ?? Array.Empty<Expression>();
                string? format = (methodArgs[0] as ConstantExpression)?.Value.ToString();

                //Unpack the array, if one exists (happens with 4+ parameters)
                if (methodArgs.Length > 1 && methodArgs[1].NodeType == ExpressionType.NewArrayInit)
                {
                    var arrayExpr = methodArgs[1] as NewArrayExpression;
                    var elements = arrayExpr.Expressions.ToArray();
                    Array.Resize(ref methodArgs, elements.Length + 1);
                    Array.Copy(elements, 0, methodArgs, 1, elements.Length);
                }

                int endIndex = format.IndexOf('?'); //Don't include params
                if (endIndex == -1)
                {
                    endIndex = format.Length;
                }

                int lastIndex = 0;
                while (true)
                {
                    int leftIndex = format.IndexOf('{', lastIndex);
                    if (leftIndex == -1 || leftIndex > endIndex)
                    {
                        builder.Append(format, lastIndex, endIndex - lastIndex);
                        break;
                    }
                    builder.Append(format, lastIndex, leftIndex - lastIndex);
                    int rightIndex = format.IndexOf('}', leftIndex);

                    int argId = int.Parse(format.Substring(leftIndex + 1, rightIndex - leftIndex - 1), NumberStyles.None, CultureInfo.InvariantCulture);
                    string fieldName = GetFieldName(methodArgs[argId + 1]);

                    var mappedId = BucketIds.GetIndex(fieldName);

                    if (!mappedId.HasValue && rightIndex != endIndex && format.Length > rightIndex + 1 && format[rightIndex + 1] == '/') //Ignore the next slash
                    {
                        rightIndex++;
                    }

                    if (mappedId.HasValue)
                    {
                        builder.Append($"{{{mappedId.Value}}}");
                    }

                    lastIndex = rightIndex + 1;
                }
                if (builder[builder.Length - 1] == '/')
                {
                    builder.Remove(builder.Length - 1, 1);
                }

                format = builder.ToString();

                return x => BucketId.Create(x.HttpMethod, string.Format(format, x.ToArray()), x.ToMajorParametersDictionary());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to generate the bucket id for this operation.", ex);
            }
        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8604 // Possible null reference argument.

        private static string GetFieldName(Expression expr)
        {
            if (expr.NodeType == ExpressionType.Convert)
            {
                expr = ((UnaryExpression)expr).Operand;
            }

            if (expr.NodeType != ExpressionType.MemberAccess)
            {
                throw new InvalidOperationException("Unsupported expression");
            }

            var memberExpr = expr as MemberExpression;
            if (memberExpr == null)
            {
                throw new InvalidOperationException("Expression is not a MemberExpression");
            }

            return memberExpr.Member.Name;
        }

        private static void AddBasicAuthHeader(string? username, string? password, RequestOptions options)
        {
            var basicAuthToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            options.RequestHeaders.Add("Authorization", new[] { $"Basic {basicAuthToken}" });
        }

        protected void CheckState()
        {
            if (LoginState != LoginState.LoggedIn)
            {
                throw new InvalidOperationException("Client is not logged in.");
            }
        }

        protected static double ToMilliseconds(Stopwatch stopwatch) => Math.Round((double)stopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000.0, 2);
        #endregion

        #region RPC Core
        internal Task<TResponse> SendRPCAsync<TRequest, TResponse>(TRequest payload, Func<TRequest, CallOptions, AsyncUnaryCall<TResponse>> methodCall, Expression<Func<string>> endpointExpr, BucketIds ids, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null, [CallerMemberName] string? funcName = null)
            where TResponse : class
            where TRequest : class
            => SendRPCAsync(payload, methodCall, GetEndpoint(endpointExpr), GetBucketId("POST", ids, endpointExpr, funcName ?? string.Empty), clientBucket, options);

        public async Task<TResponse> SendRPCAsync<TRequest, TResponse>(TRequest payload, Func<TRequest, CallOptions, AsyncUnaryCall<TResponse>> methodCall, string endpoint, BucketId? bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions? options = null)
            where TResponse : class
            where TRequest : class
        {
            options ??= new RequestOptions();
            options.HeaderOnly = true;
            options.BucketId = bucketId;
            var request = new RpcRequest<TRequest, TResponse>(GRPCClient, endpoint, payload, methodCall, options);
            return await SendRPCInternalAsync(request, endpoint);
        }

        private async Task<TResponse> SendRPCInternalAsync<TRequest, TResponse>(RpcRequest<TRequest, TResponse> request, string endpoint)
            where TResponse : class
            where TRequest : class
        {
            if (!request.Options.IgnoreState)
            {
                CheckState();
            }

            //request.Options.RetryMode ??= DefaultRetryMode;
            request.Options.UseSystemClock ??= UseSystemClock;
            request.Options.RatelimitCallback ??= DefaultRatelimitCallback;

            var stopwatch = Stopwatch.StartNew();
            var responseStream = await RequestQueue.SendAsync(request).ConfigureAwait(false);
            stopwatch.Stop();

            double milliseconds = ToMilliseconds(stopwatch);
            await _apiSentRequestEvent.InvokeAsync("POST", endpoint, milliseconds).ConfigureAwait(false);

            return responseStream;
        }

        #endregion

        protected string SerializeJson(object value)
        {
            var sb = new StringBuilder(256);
            using (TextWriter text = new StringWriter(sb, CultureInfo.InvariantCulture))
            using (JsonWriter writer = new JsonTextWriter(text))
            {
                _serializer.Serialize(writer, value);
            }

            return sb.ToString();
        }

        public async Task DeleteAccountAsync()
        {
            var bucket = new BucketIds();
            await SendRPCAsync(new Empty(), (req, opts) => GRPCClient.Client.DeleteAccountAsync(req, opts), () => "/v2/account", bucket);
        }

        public Task<Account> GetAccountAsync()
        {
            var bucket = new BucketIds();
            return SendRPCAsync(new Empty(), (req, opts) => GRPCClient.Client.GetAccountAsync(req, opts), () => "/v2/account", bucket);
        }

        public Task UpdateAccountAsync(UpdateAccountRequest body)
        {
            Check.NotNull(body, nameof(body));
            var bucket = new BucketIds();
            return SendJsonNoResAsync("PUT", () => "/v2/account", body, bucket);
        }

        public async Task<AuthenticationResponse> CheckLoginRequestAsync(string basicAuthUsername, string basicAuthPassword, ConfirmLoginRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            var response = PbSession.Parser.ParseFrom(await SendJsonAsync("POST", () => "/v2/account/authenticate/checklogin", body, bucket, options: options));
            return new AuthenticationResponse
            {
                ApiUrl = response.ApiUrl,
                Created = response.Created,
                IsRemember = response.IsRemember,
                RefreshToken = response.RefreshToken,
                Token = response.Token,
                UserId = response.UserId,
            };
        }

        public Task ConfirmLoginAsync(ConfirmLoginRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            return SendJsonNoResAsync("POST", () => "/v2/account/authenticate/confirmlogin", body, bucket, options: options);
        }

        public async Task<LoginIDResponse> CreateQRLoginAsync(string basicAuthUsername, string basicAuthPassword, LoginIDRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            var response = Protobuf.Api.LoginIDResponse.Parser.ParseFrom(await SendJsonAsync("POST", () => "/v2/account/authenticate/createqrlogin", body, bucket, options: options));
            return new LoginIDResponse
            {
                Address = response.Address,
                CreateTimeSecond = response.CreateTimeSeconds,
                LoginId = response.LoginId,
                Platform = response.Platform,
                Status = response.Status,
                UserId = response.UserId,
            };
        }

        public async Task<AuthenticationResponse> AuthenticateEmailAsync(string basicAuthUsername, string basicAuthPassword, EmailAuthenticationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            options.IgnoreState = true;
            var bucket = new BucketIds();
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            Expression<Func<string>> endpoint = () => $"/v2/account/authenticate/email";
            var response = PbSession.Parser.ParseFrom(await SendJsonAsync("POST", endpoint, body, bucket, options: options));
            return new AuthenticationResponse
            {
                ApiUrl = response.ApiUrl,
                Created = response.Created,
                IsRemember = response.IsRemember,
                RefreshToken = response.RefreshToken,
                Token = response.Token,
                UserId = response.UserId,
            };
        }

        public async Task<AuthenticationResponse> AuthenticateMezonAsync(string basicAuthUsername, string basicAuthPassword, AccountMezonRequest body, AccountMezonParams args, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            var queryArgs = new StringBuilder();
            if (args.Create.IsSpecified)
            {
                queryArgs.Append("create=")
                    .Append(args.Create.Value);
            }
            if (args.IsRemember.IsSpecified)
            {
                queryArgs.Append("&is_remember=")
                    .Append(args.IsRemember.Value);
            }
            if (args.Username.IsSpecified)
            {
                queryArgs.Append("&username=")
                    .Append(args.Username.Value);
            }

            Expression<Func<string>> endpoint = () => $"/v2/account/authenticate/mezon?{queryArgs.ToString()}";
            var response = PbSession.Parser.ParseFrom(await SendJsonAsync("POST", endpoint, body, bucket, options: options));
            return new AuthenticationResponse
            {
                ApiUrl = response.ApiUrl,
                Created = response.Created,
                IsRemember = response.IsRemember,
                RefreshToken = response.RefreshToken,
                Token = response.Token,
                UserId = response.UserId,
            };
        }

        public async Task<AccountConfirmResponse> AuthenticateSMSOTPAsync(string basicAuthUsername, string basicAuthPassword, AuthenticateSMSRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options ??= RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });

            Expression<Func<string>> endpoint = () => "/v2/account/authenticate/emailotp";
            var response = LinkAccountConfirmRequest.Parser.ParseFrom(await SendJsonAsync("POST", endpoint, body, bucket, options: options));
            return new AccountConfirmResponse
            {
                RequestId = response.ReqId,
                Status = response.Status,
                OTP = response.OtpCode
            };
        }

        //public Task LinkEmailAsync(string bearerToken, AccountEmailRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<object>("/v2/account/link/email", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task LinkMezonAsync(string bearerToken, AccountMezonRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<object>("/v2/account/link/mezon", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task<AuthenticationResponse> RegisterEmailAsync(string bearerToken, RegistrationEmailRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<AuthenticationResponse>("/v2/account/registry", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        public async Task<AuthenticationResponse> RefreshSessionAsync(string basicAuthUsername, string basicAuthPassword, SessionRefreshRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            var request = new Protobuf.Api.SessionRefreshRequest();
            request.IsRemember = body.IsRemember ?? false;
            request.Token = body.Token;
            request.Vars.Add(body.Vars ?? new Dictionary<string, string>());
            var response = await SendRPCAsync(request, (req, opts) => GRPCClient.Client.SessionRefreshAsync(req, opts), () => "/v2/account/session/refresh", bucket);
            return new AuthenticationResponse
            {
                ApiUrl = response.ApiUrl,
                Created = response.Created,
                IsRemember = response.IsRemember,
                RefreshToken = response.RefreshToken,
                Token = response.Token,
                UserId = response.UserId,
            };
        }

        //public Task UnlinkEmailAsync(string bearerToken, AccountEmailRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<object>("/v2/account/unlink/email", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task UnlinkMezonAsync(string bearerToken, AccountMezonRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<object>("/v2/account/unlink/mezon", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task<UserActivitiesResponse> GetActivitiesAsync(string bearerToken) =>
        //    SendRequestAsync<UserActivitiesResponse>("/v2/activity", HttpMethod.Get, bearerToken: bearerToken);

        //public Task<UserActivityResponse> CreateActiviyAsync(string bearerToken, CreateActivityRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<UserActivityResponse>("/v2/activity", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task<AppResponse> AddAppAsync(string bearerToken, AddAppRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<AppResponse>("/v2/apps/add", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task<AppsResponse> GetAppsAsync(string bearerToken, string filter = null, bool? tombstones = null, string cursor = null)
        //{
        //    var queryParams = new Dictionary<string, object>
        //    {
        //        { "filter", filter },
        //        { "tombstones", tombstones },
        //        { "cursor", cursor }
        //    };
        //    return SendRequestAsync<AppsResponse>("/v2/apps/app", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //// Add an application to a clan
        //public Task AddAppToClanAsync(string bearerToken, string appId, string clanId)
        //{
        //    if (string.IsNullOrEmpty(appId)) throw new ArgumentNullException(nameof(appId));
        //    if (string.IsNullOrEmpty(clanId)) throw new ArgumentNullException(nameof(clanId));
        //    var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(appId)}/clan/{Uri.EscapeDataString(clanId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken);
        //}

        //public Task DeleteAppAsync(string bearerToken, string id, bool? recordDeletion = null)
        //{
        //    if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        //    var queryParams = new Dictionary<string, object>
        //    {
        //        { "record_deletion", recordDeletion }
        //    };
        //    var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task<AppResponse> GetAppAsync(string bearerToken, string id)
        //{
        //    if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        //    var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}";
        //    return SendRequestAsync<AppResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken);
        //}

        //public Task<AppResponse> UpdateAppAsync(string bearerToken, string id, MezonUpdateAppRequest body)
        //{
        //    if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        //    Check.NotNull(body, nameof(body));
        //    var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}";
        //    return SendRequestAsync<AppResponse>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task BanAppAsync(string bearerToken, string id)
        //{
        //    if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        //    var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}/ban";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken);
        //}

        //public Task UnbanAppAsync(string bearerToken, string id)
        //{
        //    if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
        //    var urlPath = $"/v2/apps/app/{Uri.EscapeDataString(id)}/unban";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Post, bearerToken: bearerToken);
        //}

        //public Task<AuditLogsResponse> GetAuditLogsAsync(string bearerToken, string actionLog = null, string userId = null, string clanId = null, string dateLog = null)
        //{
        //    var queryParams = new Dictionary<string, object>
        //{
        //    { "action_log", actionLog },
        //    { "user_id", userId },
        //    { "clan_id", clanId },
        //    { "date_log", dateLog }
        //};
        //    return SendRequestAsync<AuditLogsResponse>("/v2/audit_log", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task UpdateCategoryOrderAsync(string bearerToken, UpdateCategoryOrdersRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<object>("/v2/category/orders", HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task<CategoryDescriptionsResponse> GetCategoryDescriptionsAsync(string bearerToken, string clanId, string creatorId = null, string categoryName = null, string categoryId = null, int? categoryOrder = null)
        //{
        //    if (string.IsNullOrEmpty(clanId)) throw new ArgumentNullException(nameof(clanId));
        //    var queryParams = new Dictionary<string, object>
        //{
        //    { "creator_id", creatorId },
        //    { "category_name", categoryName },
        //    { "category_id", categoryId },
        //    { "category_order", categoryOrder }
        //};
        //    var urlPath = $"/v2/categorydesc/{Uri.EscapeDataString(clanId)}";
        //    return SendRequestAsync<CategoryDescriptionsResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        public async Task<AuthenticationResponse> AuthenticateAppAsync(string basicAuthUsername, string basicAuthPassword, AppAuthenticationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options = RequestOptions.CreateOrClone(options);
            options.IgnoreState = true;
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            var bucket = new BucketIds();
            var response = PbSession.Parser.ParseFrom(await SendJsonAsync("POST", () => "/v2/apps/authenticate/token", body, bucket, options: options));
            return new AuthenticationResponse
            {
                ApiUrl = response.ApiUrl,
                Created = response.Created,
                IsRemember = response.IsRemember,
                RefreshToken = response.RefreshToken,
                Token = response.Token,
                UserId = response.UserId,
            };
        }

        public async Task<bool> AuthenticateAppLogoutAsync(AppAuthenticationLogoutRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();
            Expression<Func<string>> endpoint = () => $"/v2/apps/authenticate/token";
            var request = new SessionLogoutRequest();
            request.Token = body.Token;
            request.RefreshToken = body.RefreshToken;
            request.DeviceId = body.DeviceId;
            request.Platform = body.Platform;
            await SendRPCAsync(request, (req, opts) => GRPCClient.Client.SessionLogoutAsync(req, opts), endpoint, bucket);
            return true;
        }

        public Task<ClanDescList> ListClanDescsAsync(PaginationParams args, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            Expression<Func<string>> endpoint = () => $"/v2/clandesc";
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();
            var request = new ListClanDescRequest();
            request.Limit = args.Limit.GetValueOrDefault(50);
            request.State = args.State.GetValueOrDefault(0);
            request.Cursor = args.Cursor.GetValueOrDefault(string.Empty);
            return SendRPCAsync(request, (req, opts) => GRPCClient.Client.ListClanDescsAsync(req, opts), endpoint, bucket);
        }

        #region Friends

        public Task<Protobuf.Api.AddFriendsResponse> AddFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Protobuf.Api.AddFriendsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }
            if (usernames != null)
            {
                foreach (var username in usernames)
                {
                    request.Usernames.Add(username);
                }
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.AddFriendsAsync(req, opts),
                () => "/v2/friend",
                bucket);
        }

        public async Task BlockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Protobuf.Api.BlockFriendsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }
            if (usernames != null)
            {
                foreach (var username in usernames)
                {
                    request.Usernames.Add(username);
                }
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.BlockFriendsAsync(req, opts),
                () => "/v2/friend/block",
                bucket);
        }

        public async Task UnblockFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Protobuf.Api.BlockFriendsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }
            if (usernames != null)
            {
                foreach (var username in usernames)
                {
                    request.Usernames.Add(username);
                }
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.UnblockFriendsAsync(req, opts),
                () => "/v2/friend/unblock",
                bucket);
        }

        public async Task DeleteFriendsAsync(IEnumerable<long>? ids = null, IEnumerable<string>? usernames = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Protobuf.Api.DeleteFriendsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }
            if (usernames != null)
            {
                foreach (var username in usernames)
                {
                    request.Usernames.Add(username);
                }
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteFriendsAsync(req, opts),
                () => "/v2/friend",
                bucket);
        }

        public Task<FriendList> ListFriendsAsync(int? state = null, int? limit = null, string? cursor = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Protobuf.Api.ListFriendsRequest();
            if (state.HasValue)
            {
                request.State = state.Value;
            }

            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListFriendsAsync(req, opts),
                () => "/v2/friend",
                bucket);
        }

        #endregion

        #region Clan

        public Task<ClanDesc> CreateClanDescAsync(string clanName, string? logo = null, string? banner = null, RequestOptions? options = null)
        {
            Check.NotNullOrEmpty(clanName, nameof(clanName));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new CreateClanDescRequest();
            request.ClanName = clanName;
            if (!string.IsNullOrEmpty(logo))
            {
                request.Logo = logo;
            }

            if (!string.IsNullOrEmpty(banner))
            {
                request.Banner = banner;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.CreateClanDescAsync(req, opts),
                () => "/v2/clandesc",
                bucket);
        }

        public async Task DeleteClanDescAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new DeleteClanDescRequest();
            request.ClanDescId = clanId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteClanDescAsync(req, opts),
                () => $"/v2/clandesc/{clanId}",
                bucket);
        }

        public async Task UpdateClanDescAsync(UpdateClanDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateClanDescAsync(req, opts),
                () => $"/v2/clandesc/{body.ClanId}",
                bucket);
        }

        public Task<ClanUserList> ListClanUsersAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new ListClanUsersRequest();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListClanUsersAsync(req, opts),
                () => $"/v2/clandesc/{clanId}/user",
                bucket);
        }

        public async Task RemoveClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new RemoveClanUsersRequest();
            request.ClanId = clanId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.RemoveClanUsersAsync(req, opts),
                () => $"/v2/clandesc/{clanId}/kick",
                bucket);
        }

        public async Task BanClanUsersAsync(long clanId, long channelId, IEnumerable<long> userIds, int? banTime = null, string? reason = null, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new BanClanUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }

            if (banTime.HasValue)
            {
                request.BanTime = banTime.Value;
            }

            if (!string.IsNullOrEmpty(reason))
            {
                request.Reason = reason;
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.BanClanUsersAsync(req, opts),
                () => $"/v2/clandesc/{clanId}/ban",
                bucket);
        }

        #endregion

        #region Channel

        public Task<ChannelDescription> CreateChannelDescAsync(CreateChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateChannelDescAsync(req, opts),
                () => "/v2/channeldesc",
                bucket);
        }

        public async Task DeleteChannelDescAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new DeleteChannelDescRequest();
            request.ChannelId = channelId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteChannelDescAsync(req, opts),
                () => $"/v2/channeldesc/{channelId}",
                bucket);
        }

        public async Task UpdateChannelDescAsync(UpdateChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateChannelDescAsync(req, opts),
                () => $"/v2/channeldesc/{body.ChannelId}",
                bucket);
        }

        public async Task AddChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new AddChannelUsersRequest();
            request.ChannelId = channelId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.AddChannelUsersAsync(req, opts),
                () => $"/v2/channel/{channelId}/add",
                bucket);
        }

        public async Task RemoveChannelUsersAsync(long channelId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new RemoveChannelUsersRequest();
            request.ChannelId = channelId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.RemoveChannelUsersAsync(req, opts),
                () => $"/v2/channel/{channelId}/remove",
                bucket);
        }

        public Task<ChannelMessageList> ListChannelMessagesAsync(long clanId, long channelId, long? messageId = null, int? direction = null, int? limit = null, long? topicId = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new ListChannelMessagesRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            if (messageId.HasValue)
            {
                request.MessageId = messageId.Value;
            }

            if (direction.HasValue)
            {
                request.Direction = direction.Value;
            }

            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }

            if (topicId.HasValue)
            {
                request.TopicId = topicId.Value;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListChannelMessagesAsync(req, opts),
                () => $"/v2/channel/{channelId}",
                bucket);
        }

        public Task<ChannelUserList> ListChannelUsersAsync(long clanId, long channelId, int channelType, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }

            if (state.HasValue)
            {
                request.State = state.Value;
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListChannelUsersAsync(req, opts),
                () => $"/v2/channel/{channelId}/user",
                bucket);
        }

        #endregion

        #region Roles

        public Task<Mezon.Protobuf.Api.Role> CreateRoleAsync(Mezon.Protobuf.Api.CreateRoleRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateRoleAsync(req, opts),
                () => "/v2/roles",
                bucket);
        }

        public async Task DeleteRoleAsync(long roleId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeleteRoleRequest();
            request.RoleId = roleId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteRoleAsync(req, opts),
                () => $"/v2/roles/{roleId}",
                bucket);
        }

        public async Task UpdateRoleAsync(Mezon.Protobuf.Api.UpdateRoleRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateRoleAsync(req, opts),
                () => $"/v2/roles/{body.RoleId}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.RoleListEventResponse> ListRolesAsync(long? clanId = null, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.RoleListEventRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }

            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }

            if (state.HasValue)
            {
                request.State = state.Value;
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListRolesAsync(req, opts),
                () => "/v2/roles",
                bucket);
        }

        #endregion

        #region Users

        public async Task UpdateUserAsync(Mezon.Protobuf.Api.UpdateUsersRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateUserAsync(req, opts),
                () => "/v2/user/update",
                bucket);
        }

        #endregion

        #region Events

        public Task<Mezon.Protobuf.Api.EventManagement> CreateEventAsync(Mezon.Protobuf.Api.CreateEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateEventAsync(req, opts),
                () => "/v2/eventmanagement/create",
                bucket);
        }

        public async Task DeleteEventAsync(long eventId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeleteEventRequest();
            request.EventId = eventId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteEventAsync(req, opts),
                () => $"/v2/eventmanagement/{eventId}",
                bucket);
        }

        public async Task UpdateEventAsync(Mezon.Protobuf.Api.UpdateEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateEventAsync(req, opts),
                () => $"/v2/eventmanagement/{body.EventId}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.EventList> ListEventsAsync(long? clanId = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListEventsRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListEventsAsync(req, opts),
                () => "/v2/eventmanagement",
                bucket);
        }

        #endregion

        #region Messages (Advanced)

        public Task<Mezon.Protobuf.Api.SearchMessageResponse> SearchMessageAsync(Mezon.Protobuf.Api.SearchMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SearchMessageAsync(req, opts),
                () => "/v2/es/search",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.ChannelMessage> CreatePinMessageAsync(Mezon.Protobuf.Api.PinMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreatePinMessageAsync(req, opts),
                () => "/v2/pinmessage/set",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.PinMessagesList> GetPinMessagesListAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.PinMessageRequest();
            request.ChannelId = channelId;
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetPinMessagesListAsync(req, opts),
                () => "/v2/pinmessage/get",
                bucket);
        }

        public async Task DeletePinMessageAsync(long messageId, long channelId, long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeletePinMessage();
            request.MessageId = messageId;
            request.ChannelId = channelId;
            request.ClanId = clanId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeletePinMessageAsync(req, opts),
                () => "/v2/pinmessage/delete",
                bucket);
        }

        public async Task MarkAsReadAsync(Mezon.Protobuf.Api.MarkAsReadRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.MarkAsReadAsync(req, opts),
                () => "/v2/markasread",
                bucket);
        }

        #endregion

        //#region User

        //public Task<UsersResponse> GetUsersAsync(string bearerToken, IEnumerable<string>? ids = null, IEnumerable<string>? usernames = null)
        //{
        //    var queryParams = new Dictionary<string, object>();
        //    if (ids != null) queryParams["ids"] = ids;
        //    if (usernames != null) queryParams["usernames"] = usernames;

        //    return SendRequestAsync<UsersResponse>("/v2/user", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task UpdateUserStatusAsync(string bearerToken, UpdateUserStatusRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<object>("/v2/userstatus", HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task<UserStatusResponse> GetUserStatusAsync(string bearerToken)
        //{
        //    return SendRequestAsync<UserStatusResponse>("/v2/userstatus", HttpMethod.Get, bearerToken: bearerToken);
        //}

        //#endregion

        //#region Roles

        //public Task<RoleResponse> CreateRoleAsync(string bearerToken, CreateRoleRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<RoleResponse>("/v2/roles", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task DeleteRoleAsync(string bearerToken, string roleId, string channelId = null, string clanId = null, string roleLabel = null)
        //{
        //    Check.NotNullOrEmpty(roleId, nameof(roleId));
        //    var urlPath = $"/v2/roles/{Uri.EscapeDataString(roleId)}";
        //    var queryParams = new Dictionary<string, object>
        //    {
        //        { "channel_id", channelId },
        //        { "clan_id", clanId },
        //        { "role_label", roleLabel }
        //    };
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken);
        //}

        //public Task UpdateRoleAsync(string bearerToken, string roleId, UpdateRoleRequest body)
        //{
        //    Check.NotNullOrEmpty(roleId, nameof(roleId));
        //    Check.NotNull(body, nameof(body));
        //    var urlPath = $"/v2/roles/{Uri.EscapeDataString(roleId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task<RoleEventResponse> GetRolesAsync(string bearerToken, string clanId = null, int? limit = null, int? state = null, string cursor = null)
        //{
        //    var urlPath = "/v2/roles";
        //    var queryParams = new Dictionary<string, object>
        //    {
        //        { "clan_id", clanId },
        //        { "limit", limit },
        //        { "state", state },
        //        { "cursor", cursor }
        //    };
        //    return SendRequestAsync<RoleEventResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //#endregion

        //#region Notifications

        //public Task DeleteNotificationsAsync(string bearerToken, IEnumerable<string>? ids = null, string category = null)
        //{
        //    var queryParams = new Dictionary<string, object>();
        //    if (ids != null) queryParams["ids"] = ids;
        //    if (category != null) queryParams["category"] = category;

        //    return SendRequestAsync<object>("/v2/notification", HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task<NotificationsResponse> GetNotificationsAsync(string bearerToken, string clanId = null, string notificationId = null, string category = null, int? limit = null, int? direction = null)
        //{
        //    var queryParams = new Dictionary<string, object>();
        //    if (clanId != null) queryParams["clan_id"] = clanId;
        //    if (notificationId != null) queryParams["notification_id"] = notificationId;
        //    if (category != null) queryParams["category"] = category;
        //    if (limit != null) queryParams["limit"] = limit;
        //    if (direction != null) queryParams["direction"] = direction;

        //    return SendRequestAsync<NotificationsResponse>("/v2/notification", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //#endregion

        //#region Storage

        //public Task<UploadAttachmentResponse> UploadAttachmentFileAsync(string bearerToken, UploadAttachmentRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<UploadAttachmentResponse>("/v2/uploadattachmentfile", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //#endregion

        //#region Category
        //public Task<CategoryDescriptionResponse> CreateCategoryDescriptionAsync(string bearerToken, CreateCategoryDescriptionRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<CategoryDescriptionResponse>("/v2/createcategory", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task DeleteCategoryDescriptionAsync(string bearerToken, string categoryId, string clanId, string categoryLabel = null)
        //{
        //    Check.NotNullOrEmpty(categoryId, nameof(categoryId));
        //    Check.NotNullOrEmpty(clanId, nameof(clanId));
        //    var queryParams = new Dictionary<string, object>();
        //    if (categoryLabel != null) queryParams["category_label"] = categoryLabel;

        //    var urlPath = $"/v2/deletecategory/category_id/{Uri.EscapeDataString(categoryId)}/clan_id/{Uri.EscapeDataString(clanId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task UpdateCategoryAsync(string bearerToken, string clanId, UpdateCategoryRequest body)
        //{
        //    Check.NotNullOrEmpty(clanId, nameof(clanId));
        //    Check.NotNull(body, nameof(body));
        //    var urlPath = $"/v2/categorydesc/{Uri.EscapeDataString(clanId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}
        //#endregion

        //#region Events
        //public Task<EventManagementResponse> CreateEventAsync(string bearerToken, CreateEventRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<EventManagementResponse>("/v2/eventmanagement/create", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task DeleteEventAsync(string bearerToken, string eventId, string clanId, string creatorId, string eventLabel = null, string channelId = null)
        //{
        //    Check.NotNullOrEmpty(eventId, nameof(eventId));
        //    var queryParams = new Dictionary<string, object>
        //    {
        //        { "clan_id", clanId }, { "creator_id", creatorId }, { "event_label", eventLabel }, { "channel_id", channelId }
        //    };
        //    var urlPath = $"/v2/event/{Uri.EscapeDataString(eventId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task UpdateEventUserAsync(string bearerToken, UpdateEventUserRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    var urlPath = "/v2/eventmanagement/user";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task UpdateEventAsync(string bearerToken, string eventId, UpdateEventRequest body)
        //{
        //    Check.NotNullOrEmpty(eventId, nameof(eventId));
        //    Check.NotNull(body, nameof(body));
        //    var urlPath = $"/v2/eventmanagement/{Uri.EscapeDataString(eventId)}";
        //    return SendRequestAsync<object>(urlPath, HttpMethod.Put, bearerToken: bearerToken, body: body);
        //}

        //public Task<EventManagementsResponse> GetEventsAsync(string bearerToken, string clanId = null)
        //{
        //    var queryParams = new Dictionary<string, object>();
        //    if (clanId != null) queryParams["clan_id"] = clanId;
        //    return SendRequestAsync<EventManagementsResponse>("/v2/eventmanagement", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task AddUserEventAsync(string bearerToken, AddUserEventRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<object>("/v2/userevent", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task DeleteUserEventAsync(string bearerToken, string clanId, string eventId)
        //{
        //    Check.NotNullOrEmpty(clanId, nameof(clanId));
        //    Check.NotNullOrEmpty(eventId, nameof(eventId));
        //    var queryParams = new Dictionary<string, object>
        //    {
        //        { "clan_id", clanId },
        //        { "event_id", eventId }
        //    };
        //    return SendRequestAsync<object>("/v2/userevent", HttpMethod.Delete, bearerToken: bearerToken, queryParams: queryParams);
        //}
        //#endregion

        //#region Permissions
        //public Task<PermissionsResponse> GetPermissionsAsync(string bearerToken) =>
        //    SendRequestAsync<PermissionsResponse>("/v2/permissions", HttpMethod.Get, bearerToken: bearerToken);

        //public Task<PermissionsResponse> GetRolePermissionsAsync(string bearerToken, string roleId)
        //{
        //    Check.NotNullOrEmpty(roleId, nameof(roleId));
        //    var urlPath = $"/v2/roles/{Uri.EscapeDataString(roleId)}/permissions";
        //    return SendRequestAsync<PermissionsResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken);
        //}

        //public Task<RoleUsersResponse> ListRoleUsersAsync(string bearerToken, string roleId, int? limit = null, string cursor = null)
        //{
        //    Check.NotNullOrEmpty(roleId, nameof(roleId));
        //    var queryParams = new Dictionary<string, object>();
        //    if (limit != null) queryParams["limit"] = limit;
        //    if (cursor != null) queryParams["cursor"] = cursor;
        //    var urlPath = $"/v2/roles/{Uri.EscapeDataString(roleId)}/users";
        //    return SendRequestAsync<RoleUsersResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task<UserPermissionsInChannelResponse> GetUserPermissionsInChannelAsync(string bearerToken, string clanId, string channelId)
        //{
        //    var queryParams = new Dictionary<string, object>
        //    {
        //        { "clan_id", clanId },
        //        { "channel_id", channelId }
        //    };
        //    return SendRequestAsync<UserPermissionsInChannelResponse>("/v2/users/clans/channels", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}
        //#endregion

        //#region Invites
        //public Task<LinkInviteUserResponse> CreateLinkInviteUserAsync(string bearerToken, LinkInviteUserRequest body)
        //{
        //    Check.NotNull(body, nameof(body));
        //    return SendRequestAsync<LinkInviteUserResponse>("/v2/invite", HttpMethod.Post, bearerToken: bearerToken, body: body);
        //}

        //public Task<InviteUserResponse> GetLinkInviteAsync(string basicAuthUsername, string basicAuthPassword, string inviteId)
        //{
        //    Check.NotNullOrEmpty(inviteId, nameof(inviteId));
        //    var urlPath = $"/v2/invite/{Uri.EscapeDataString(inviteId)}";
        //    return SendRequestWithBasicAuthAsync<InviteUserResponse>(urlPath, HttpMethod.Get, basicAuthUsername: basicAuthUsername, basicAuthPassword: basicAuthPassword);
        //}

        //public Task<InviteUserResponse> InviteUserAsync(string bearerToken, string inviteId)
        //{
        //    Check.NotNullOrEmpty(inviteId, nameof(inviteId));
        //    var urlPath = $"/v2/invite/{Uri.EscapeDataString(inviteId)}";
        //    return SendRequestAsync<InviteUserResponse>(urlPath, HttpMethod.Post, bearerToken: bearerToken);
        //}
        //#endregion

        //#region Notification Settings
        //public Task SetNotificationClanSettingAsync(string bearerToken, SetDefaultNotificationRequest body) =>
        //    SendRequestAsync<object>("/v2/notificationclan/set", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task SetNotificationChannelSettingAsync(string bearerToken, SetNotificationChannelRequest body) =>
        //    SendRequestAsync<object>("/v2/notificationchannel/set", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task SetMuteNotificationCategoryAsync(string bearerToken, SetMuteNotificationRequest body) =>
        //    SendRequestAsync<object>("/v2/mutenotificationcategory/set", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task SetMuteNotificationChannelAsync(string bearerToken, SetMuteNotificationRequest body) =>
        //    SendRequestAsync<object>("/v2/mutenotificationchannel/set", HttpMethod.Post, bearerToken: bearerToken, body: body);

        //public Task<NotificationChannelCategorySettingsResponse> GetChannelCategoryNotificationSettingsAsync(string bearerToken, string clanId)
        //{
        //    var queryParams = new Dictionary<string, object> { { "clan_id", clanId } };
        //    return SendRequestAsync<NotificationChannelCategorySettingsResponse>("/v2/getnotificationchannel", HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}

        //public Task<ClanNotificationSettingResponse> GetClanNotificationSettingAsync(string bearerToken, string clanId)
        //{
        //    Check.NotNullOrEmpty(clanId, nameof(clanId));
        //    var urlPath = "/v2/getnotificationclan";
        //    var queryParams = new Dictionary<string, object> { { "clan_id", clanId } };
        //    return SendRequestAsync<ClanNotificationSettingResponse>(urlPath, HttpMethod.Get, bearerToken: bearerToken, queryParams: queryParams);
        //}
        //#endregion

        #region Emoji & Stickers

        public async Task CreateClanEmojiAsync(Mezon.Protobuf.Api.ClanEmojiCreateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateClanEmojiAsync(req, opts),
                () => "/v2/emoji/create",
                bucket);
        }

        public async Task UpdateClanEmojiByIdAsync(Mezon.Protobuf.Api.ClanEmojiUpdateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateClanEmojiByIdAsync(req, opts),
                () => $"/v2/emoji/{body.Id}",
                bucket);
        }

        public async Task DeleteClanEmojiByIdAsync(long emojiId, long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ClanEmojiDeleteRequest();
            request.Id = emojiId;
            request.ClanId = clanId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteByIdClanEmojiAsync(req, opts),
                () => $"/v2/emoji/{emojiId}",
                bucket);
        }

        public async Task AddClanStickerAsync(Mezon.Protobuf.Api.ClanStickerAddRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.AddClanStickerAsync(req, opts),
                () => "/v2/sticker",
                bucket);
        }

        public async Task UpdateClanStickerByIdAsync(Mezon.Protobuf.Api.ClanStickerUpdateByIdRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateClanStickerByIdAsync(req, opts),
                () => $"/v2/sticker/{body.Id}",
                bucket);
        }

        public async Task DeleteClanStickerByIdAsync(long stickerId, long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ClanStickerDeleteRequest();
            request.Id = stickerId;
            request.ClanId = clanId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteClanStickerByIdAsync(req, opts),
                () => $"/v2/sticker/{stickerId}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.EmojiListedResponse> GetListEmojisByUserIdAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.GetListEmojisByUserIdAsync(req, opts),
                () => "/v2/emojis",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.StickerListedResponse> GetListStickersByUserIdAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.GetListStickersByUserIdAsync(req, opts),
                () => "/v2/stickers",
                bucket);
        }

        #endregion

        #region Webhooks

        public Task<Mezon.Protobuf.Api.WebhookGenerateResponse> GenerateWebhookAsync(Mezon.Protobuf.Api.WebhookCreateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.GenerateWebhookAsync(req, opts),
                () => "/v2/webhooks/generate",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.WebhookListResponse> ListWebhookByChannelIdAsync(long channelId, long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.WebhookListRequest();
            request.ChannelId = channelId;
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListWebhookByChannelIdAsync(req, opts),
                () => $"/v2/webhooks/{channelId}",
                bucket);
        }

        public async Task UpdateWebhookByIdAsync(Mezon.Protobuf.Api.WebhookUpdateRequestById body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateWebhookByIdAsync(req, opts),
                () => $"/v2/webhooks/update/{body.Id}",
                bucket);
        }

        public async Task DeleteWebhookByIdAsync(Mezon.Protobuf.Api.WebhookDeleteRequestById body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.DeleteWebhookByIdAsync(req, opts),
                () => $"/v2/webhooks/{body.Id}",
                bucket);
        }

        #endregion

        #region System Messages

        public async Task CreateSystemMessageAsync(Mezon.Protobuf.Api.SystemMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateSystemMessageAsync(req, opts),
                () => "/v2/systemmessages",
                bucket);
        }

        public async Task UpdateSystemMessageAsync(Mezon.Protobuf.Api.SystemMessageRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateSystemMessageAsync(req, opts),
                () => $"/v2/systemmessages/{body.ClanId}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.SystemMessage> GetSystemMessageByClanIdAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.GetSystemMessage();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetSystemMessageByClanIdAsync(req, opts),
                () => $"/v2/systemmessages/{clanId}",
                bucket);
        }

        public async Task DeleteSystemMessageAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeleteSystemMessage();
            request.ClanId = clanId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteSystemMessageAsync(req, opts),
                () => $"/v2/systemmessages/{clanId}",
                bucket);
        }

        #endregion

        #region Ordering

        public async Task UpdateRoleOrderAsync(Mezon.Protobuf.Api.UpdateRoleOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateRoleOrderAsync(req, opts),
                () => "/v2/role/orders",
                bucket);
        }

        public async Task UpdateClanOrderAsync(Mezon.Protobuf.Api.UpdateClanOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateClanOrderAsync(req, opts),
                () => "/v2/updateclanorder",
                bucket);
        }

        #endregion

        #region Encryption

        public Task<Mezon.Protobuf.Api.ChanEncryptionMethod> GetChanEncryptionMethodAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ChanEncryptionMethod();
            request.ChannelId = channelId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetChanEncryptionMethodAsync(req, opts),
                () => $"/v2/channel/{channelId}/encrypt_method",
                bucket);
        }

        public async Task SetChanEncryptionMethodAsync(Mezon.Protobuf.Api.ChanEncryptionMethod body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SetChanEncryptionMethodAsync(req, opts),
                () => $"/v2/channel/{body.ChannelId}/encrypt_method",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.GetPubKeysResponse> GetPublicKeysAsync(IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.GetPubKeysRequest();
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetPubKeysAsync(req, opts),
                () => "/v2/pubkey",
                bucket);
        }

        public async Task PushPublicKeyAsync(Mezon.Protobuf.Api.PushPubKeyRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.PushPubKeyAsync(req, opts),
                () => "/v2/pubkey/push",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.GetKeyServerResp> GetKeyServerAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.GetKeyServerAsync(req, opts),
                () => "/v2/e2ee/key_server",
                bucket);
        }

        #endregion

        #region Onboarding

        public Task<Mezon.Protobuf.Api.ListOnboardingResponse> ListOnboardingAsync(long clanId, int? guideType = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListOnboardingRequest();
            request.ClanId = clanId;
            if (guideType.HasValue)
            {
                request.GuideType = guideType.Value;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListOnboardingAsync(req, opts),
                () => "/v2/onboarding",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.OnboardingItem> GetOnboardingDetailAsync(long id, long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.OnboardingRequest();
            request.Id = id;
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetOnboardingDetailAsync(req, opts),
                () => $"/v2/onboarding/{id}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.ListOnboardingResponse> CreateOnboardingAsync(Mezon.Protobuf.Api.CreateOnboardingRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateOnboardingAsync(req, opts),
                () => "/v2/onboarding",
                bucket);
        }

        public async Task UpdateOnboardingAsync(Mezon.Protobuf.Api.UpdateOnboardingRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateOnboardingAsync(req, opts),
                () => $"/v2/onboarding/{body.Id}",
                bucket);
        }

        public async Task DeleteOnboardingAsync(long id, long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.OnboardingRequest();
            request.Id = id;
            request.ClanId = clanId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteOnboardingAsync(req, opts),
                () => $"/v2/onboarding/{id}",
                bucket);
        }

        #endregion

        #region Activity

        public Task<Mezon.Protobuf.Api.ListUserActivity> ListActivityAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.ListActivityAsync(req, opts),
                () => "/v2/activity",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.UserActivity> CreateActivityAsync(Mezon.Protobuf.Api.CreateActivityRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateActiviyAsync(req, opts),
                () => "/v2/activity",
                bucket);
        }

        #endregion

        #region Mezon Meet

        public Task<Mezon.Protobuf.Api.GenerateMezonMeetResponse> CreateExternalMezonMeetAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.CreateExternalMezonMeetAsync(req, opts),
                () => "/v2/meet/external/create",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.GenerateMeetTokenResponse> GenerateMeetTokenAsync(Mezon.Protobuf.Api.GenerateMeetTokenRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.GenerateMeetTokenAsync(req, opts),
                () => "/v2/meet/generate",
                bucket);
        }

        #endregion

        #region Ownership

        public async Task TransferOwnershipAsync(Mezon.Protobuf.Api.TransferOwnershipRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.TransferOwnershipAsync(req, opts),
                () => "/v2/transfer/ownership",
                bucket);
        }

        #endregion

        #region Permissions

        public Task<Mezon.Protobuf.Api.PermissionList> GetListPermissionAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.GetListPermissionAsync(req, opts),
                () => "/v2/permissions",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.PermissionList> ListRolePermissionsAsync(long roleId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListPermissionsRequest();
            request.RoleId = roleId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListRolePermissionsAsync(req, opts),
                () => $"/v2/roles/{roleId}/permissions",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.RoleUserList> ListRoleUsersAsync(long roleId, int? limit = null, string? cursor = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListRoleUsersRequest();
            request.RoleId = roleId;
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListRoleUsersAsync(req, opts),
                () => $"/v2/roles/{roleId}/users",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.UserPermissionInChannelListResponse> ListUserPermissionInChannelAsync(long clanId, long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.UserPermissionInChannelListRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListUserPermissionInChannelAsync(req, opts),
                () => "/v2/users/clans/channels",
                bucket);
        }

        #endregion

        #region Notifications

        public async Task DeleteNotificationsAsync(IEnumerable<long>? ids = null, int? category = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeleteNotificationsRequest();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    request.Ids.Add(id);
                }
            }

            if (category.HasValue)
            {
                request.Category = category.Value;
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteNotificationsAsync(req, opts),
                () => "/v2/notification",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.NotificationList> ListNotificationsAsync(long? clanId = null, long? notificationId = null, int? limit = null, int? direction = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListNotificationsRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }

            if (notificationId.HasValue)
            {
                request.NotificationId = notificationId.Value;
            }

            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }

            if (direction.HasValue)
            {
                request.Direction = direction.Value;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListNotificationsAsync(req, opts),
                () => "/v2/notification",
                bucket);
        }

        #endregion

        #region Category

        public Task<Mezon.Protobuf.Api.CategoryDesc> CreateCategoryDescAsync(Mezon.Protobuf.Api.CreateCategoryDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateCategoryDescAsync(req, opts),
                () => "/v2/createcategory",
                bucket);
        }

        public async Task DeleteCategoryDescAsync(long categoryId, long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeleteCategoryDescRequest();
            request.CategoryId = categoryId;
            request.ClanId = clanId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteCategoryDescAsync(req, opts),
                () => $"/v2/deletecategory/category_id/{categoryId}/clan_id/{clanId}",
                bucket);
        }

        public async Task UpdateCategoryAsync(Mezon.Protobuf.Api.UpdateCategoryDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateCategoryAsync(req, opts),
                () => $"/v2/updatecategory/{body.ClanId}",
                bucket);
        }

        public async Task UpdateCategoryOrderAsync(Mezon.Protobuf.Api.UpdateCategoryOrderRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateCategoryOrderAsync(req, opts),
                () => "/v2/category/orders",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.CategoryDescList> ListCategoryDescsAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.CategoryDesc();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListCategoryDescsAsync(req, opts),
                () => $"/v2/categorydesc/{clanId}",
                bucket);
        }

        #endregion

        #region Invites

        public Task<Mezon.Protobuf.Api.LinkInviteUser> CreateLinkInviteUserAsync(Mezon.Protobuf.Api.LinkInviteUserRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateLinkInviteUserAsync(req, opts),
                () => "/v2/invite",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.InviteUserRes> InviteUserAsync(long inviteId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.InviteUserRequest();
            request.InviteId = inviteId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.InviteUserAsync(req, opts),
                () => $"/v2/invite/{inviteId}",
                bucket);
        }

        #endregion

        #region Notification Settings

        public async Task SetNotificationClanSettingAsync(Mezon.Protobuf.Api.SetDefaultNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SetNotificationClanSettingAsync(req, opts),
                () => "/v2/notificationclan/set",
                bucket);
        }

        public async Task SetNotificationChannelSettingAsync(Mezon.Protobuf.Api.SetNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SetNotificationChannelSettingAsync(req, opts),
                () => "/v2/notificationchannel/set",
                bucket);
        }

        public async Task SetMuteNotificationCategoryAsync(Mezon.Protobuf.Api.SetMuteRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SetMuteCategoryAsync(req, opts),
                () => "/v2/mutenotificationcategory/set",
                bucket);
        }

        public async Task SetMuteNotificationChannelAsync(Mezon.Protobuf.Api.SetMuteRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SetMuteChannelAsync(req, opts),
                () => "/v2/mutenotificationchannel/set",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.NotificationChannelCategorySettingList> GetChannelCategoryNotificationSettingsAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.NotificationClan();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetChannelCategoryNotiSettingsListAsync(req, opts),
                () => "/v2/getchannelcategorynotisettingslist",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.NotificationSetting> GetClanNotificationSettingAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.NotificationClan();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetNotificationClanAsync(req, opts),
                () => "/v2/getnotificationclan",
                bucket);
        }

        #endregion

        #region User Status

        public Task<Mezon.Protobuf.Api.UserStatus> GetUserStatusAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.GetUserStatusAsync(req, opts),
                () => "/v2/userstatus",
                bucket);
        }

        public async Task UpdateUserStatusAsync(Mezon.Protobuf.Api.UserStatusUpdate body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateUserStatusAsync(req, opts),
                () => "/v2/userstatus",
                bucket);
        }

        #endregion

        #region Apps

        public Task<Mezon.Protobuf.Api.App> AddAppAsync(Mezon.Protobuf.Api.AddAppRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.AddAppAsync(req, opts),
                () => "/v2/apps/add",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.AppList> ListAppsAsync(string? filter = null, bool? tombstones = null, string? cursor = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListAppsRequest();
            if (!string.IsNullOrEmpty(filter))
            {
                request.Filter = filter;
            }

            if (tombstones.HasValue)
            {
                request.Tombstones = tombstones.Value;
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListAppsAsync(req, opts),
                () => "/v2/apps/app",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.App> GetAppAsync(long id, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.AppId();
            request.Id = id;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetAppAsync(req, opts),
                () => $"/v2/apps/app/{id}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.App> UpdateAppAsync(Mezon.Protobuf.Api.UpdateAppRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateAppAsync(req, opts),
                () => $"/v2/apps/app/{body.Id}",
                bucket);
        }

        public async Task DeleteAppAsync(long id, bool? recordDeletion = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.AppDeleteRequest();
            request.Id = id;
            if (recordDeletion.HasValue)
            {
                request.RecordDeletion = recordDeletion.Value;
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteAppAsync(req, opts),
                () => $"/v2/apps/app/{id}",
                bucket);
        }

        public async Task AddAppToClanAsync(long appId, long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.AppClan();
            request.AppId = appId;
            request.ClanId = clanId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.AddAppToClanAsync(req, opts),
                () => $"/v2/apps/app/{appId}/clan/{clanId}",
                bucket);
        }

        #endregion

        #region Audit Log

        public Task<Mezon.Protobuf.Api.ListAuditLog> ListAuditLogAsync(long? clanId = null, string? actionLog = null, long? userId = null, string? dateLog = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListAuditLogRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }

            if (!string.IsNullOrEmpty(actionLog))
            {
                request.ActionLog = actionLog;
            }

            if (userId.HasValue)
            {
                request.UserId = userId.Value;
            }

            if (!string.IsNullOrEmpty(dateLog))
            {
                request.DateLog = dateLog;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListAuditLogAsync(req, opts),
                () => "/v2/audit_log",
                bucket);
        }

        #endregion

        #region Storage

        public Task<Mezon.Protobuf.Api.UploadAttachment> UploadAttachmentFileAsync(Mezon.Protobuf.Api.UploadAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UploadAttachmentFileAsync(req, opts),
                () => "/v2/uploadattachmentfile",
                bucket);
        }

        #endregion

        #region User Events

        public async Task AddUserEventAsync(Mezon.Protobuf.Api.UserEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.AddUserEventAsync(req, opts),
                () => "/v2/userevent",
                bucket);
        }

        public async Task DeleteUserEventAsync(long clanId, long eventId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.UserEventRequest();
            request.ClanId = clanId;
            request.EventId = eventId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteUserEventAsync(req, opts),
                () => "/v2/userevent",
                bucket);
        }

        #endregion

        #region Healthcheck

        public async Task HealthcheckAsync(RequestOptions? options = null)
        {
            options = RequestOptions.CreateOrClone(options);
            options.IgnoreState = true;
            var bucket = new BucketIds();

            await SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.HealthcheckAsync(req, opts),
                () => "/healthcheck",
                bucket);
        }

        #endregion

        #region Channel Descs

        public Task<Mezon.Protobuf.Api.ChannelDescList> ListChannelDescsAsync(long clanId, int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListChannelDescsRequest();
            request.ClanId = clanId;
            if (limit.HasValue)
            {
                request.Limit = limit.Value;
            }

            if (state.HasValue)
            {
                request.State = state.Value;
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                request.Cursor = cursor;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListChannelDescsAsync(req, opts),
                () => "/v2/channeldesc",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.ChannelDescription> GetChannelDetailAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListChannelDetailRequest();
            request.ChannelId = channelId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListChannelDetailAsync(req, opts),
                () => $"/v2/channeldesc/{channelId}",
                bucket);
        }

        #endregion

        #region Banned Users

        public Task<Mezon.Protobuf.Api.BannedUserList> ListBannedUsersAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.BannedUserListRequest();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListBannedUsersAsync(req, opts),
                () => "/v2/banned",
                bucket);
        }

        public async Task UnbanClanUsersAsync(long clanId, IEnumerable<long> userIds, RequestOptions? options = null)
        {
            Check.NotNull(userIds, nameof(userIds));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.BanClanUsersRequest();
            request.ClanId = clanId;
            foreach (var userId in userIds)
            {
                request.UserIds.Add(userId);
            }

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.UnbanClanUsersAsync(req, opts),
                () => $"/v2/clandesc/{clanId}/unban",
                bucket);
        }

        #endregion

        #region FCM Device Token

        public Task<Mezon.Protobuf.Api.RegistFcmDeviceTokenResponse> RegistFCMDeviceTokenAsync(Mezon.Protobuf.Api.RegistFcmDeviceTokenRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.RegistFCMDeviceTokenAsync(req, opts),
                () => "/v2/devicetoken",
                bucket);
        }

        #endregion

        #region User Clans

        public Task<Mezon.Protobuf.Api.AllUserClans> ListUserClansByUserIdAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.ListUserClansByUserIdAsync(req, opts),
                () => "/v2/users/clans",
                bucket);
        }

        #endregion

        #region Channel Apps

        public Task<Mezon.Protobuf.Api.ListChannelAppsResponse> ListChannelAppsAsync(long? clanId = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListChannelAppsRequest();
            if (clanId.HasValue)
            {
                request.ClanId = clanId.Value;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListChannelAppsAsync(req, opts),
                () => "/v2/channel-apps",
                bucket);
        }

        #endregion

        #region Direct Messages

        public async Task CloseDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeleteChannelDescRequest();
            request.ChannelId = channelId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.CloseDMByChannelIdAsync(req, opts),
                () => "/v2/direct/close",
                bucket);
        }

        public async Task OpenDMByChannelIdAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeleteChannelDescRequest();
            request.ChannelId = channelId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.OpenDMByChannelIdAsync(req, opts),
                () => "/v2/direct/open",
                bucket);
        }

        #endregion

        #region User Profile

        public Task<Mezon.Protobuf.Api.ClanProfile> GetUserProfileOnClanAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ClanProfileRequest();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetUserProfileOnClanAsync(req, opts),
                () => $"/v2/getclanprofile/{clanId}",
                bucket);
        }

        public async Task UpdateUserProfileByClanAsync(Mezon.Protobuf.Api.UpdateClanProfileRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateUserProfileByClanAsync(req, opts),
                () => $"/v2/updateclanprofile/{body.ClanId}",
                bucket);
        }

        #endregion

        #region Thread

        public async Task LeaveThreadAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.LeaveThreadRequest();
            request.ChannelId = channelId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.LeaveThreadAsync(req, opts),
                () => $"/v2/channel/{channelId}/leave",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.ChannelDescListNoPool> ListThreadDescsAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListThreadRequest();
            request.ChannelId = channelId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListThreadDescsAsync(req, opts),
                () => $"/v2/thread/{channelId}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.ChannelDescList> SearchThreadAsync(Mezon.Protobuf.Api.SearchThreadRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SearchThreadAsync(req, opts),
                () => "/v2/searchthread",
                bucket);
        }

        #endregion

        #region Account Linking

        public Task<Mezon.Protobuf.Api.LinkAccountConfirmRequest> LinkSMSAsync(Mezon.Protobuf.Api.AccountMezon body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.LinkSMSAsync(req, opts),
                () => "/v2/account/link/mezon",
                bucket);
        }

        public async Task ConfirmLinkMezonOTPAsync(Mezon.Protobuf.Api.LinkAccountConfirmRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.ConfirmLinkMezonOTPAsync(req, opts),
                () => "/v2/account/link/confirm",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.LinkAccountConfirmRequest> LinkEmailAsync(Mezon.Protobuf.Api.AccountEmail body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.LinkEmailAsync(req, opts),
                () => "/v2/account/link/email",
                bucket);
        }

        public async Task UnlinkMezonAsync(Mezon.Protobuf.Api.AccountMezon body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UnlinkMezonAsync(req, opts),
                () => "/v2/account/unlink/mezon",
                bucket);
        }

        public async Task UnlinkEmailAsync(Mezon.Protobuf.Api.AccountEmail body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UnlinkEmailAsync(req, opts),
                () => "/v2/account/unlink/email",
                bucket);
        }

        #endregion

        #region Banned Check

        public Task<Mezon.Protobuf.Api.IsBannedResponse> IsBannedAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.IsBannedRequest();
            request.ChannelId = channelId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.IsBannedAsync(req, opts),
                () => $"/v2/channel/{channelId}/isban",
                bucket);
        }

        #endregion

        #region Role Channel Permission

        public async Task AddRolesChannelDescAsync(Mezon.Protobuf.Api.AddRoleChannelDescRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.AddRolesChannelDescAsync(req, opts),
                () => "/v2/rolechannel/addrole",
                bucket);
        }

        public async Task DeleteRoleChannelDescAsync(long roleId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeleteRoleRequest();
            request.RoleId = roleId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteRoleChannelDescAsync(req, opts),
                () => "/v2/rolechannel/delete",
                bucket);
        }

        public async Task SetRoleChannelPermissionAsync(Mezon.Protobuf.Api.UpdateRoleChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SetRoleChannelPermissionAsync(req, opts),
                () => "/v2/permissionrolechannel/set",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.RoleList> GetRoleOfUserInTheClanAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListPermissionOfUsersRequest();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetRoleOfUserInTheClanAsync(req, opts),
                () => $"/v2/roleuserinclan/{clanId}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.PermissionRoleChannelListEventResponse> GetPermissionByRoleIdChannelIdAsync(Mezon.Protobuf.Api.PermissionRoleChannelListEventRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.GetPermissionByRoleIdChannelIdAsync(req, opts),
                () => "/v2/permissions/roles/channels/users",
                bucket);
        }

        #endregion

        #region Channel Attachments

        public Task<Mezon.Protobuf.Api.ChannelAttachmentList> ListChannelAttachmentAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListChannelAttachmentRequest();
            request.ChannelId = channelId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListChannelAttachmentAsync(req, opts),
                () => $"/v2/channel/{channelId}/attachment",
                bucket);
        }

        #endregion

        #region Voice Channel Users

        public Task<Mezon.Protobuf.Api.VoiceChannelUserList> ListChannelVoiceUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListChannelVoiceUsersAsync(req, opts),
                () => "/v2/channelvoice",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.StreamingChannelUserList> ListStreamingChannelUsersAsync(long clanId, long channelId, int channelType, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListChannelUsersRequest();
            request.ClanId = clanId;
            request.ChannelId = channelId;
            request.ChannelType = channelType;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListStreamingChannelUsersAsync(req, opts),
                () => "/v2/streaming-channels/users",
                bucket);
        }

        #endregion

        #region Channel By User

        public Task<Mezon.Protobuf.Api.ChannelDescListNoPool> ListChannelByUserIdAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.ListChannelByUserIdAsync(req, opts),
                () => "/v2/listchannelbyuserid",
                bucket);
        }

        #endregion

        #region Notification Category

        public Task<Mezon.Protobuf.Api.NotificationUserChannel> GetNotificationChannelAsync(Mezon.Protobuf.Api.NotificationChannel body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.GetNotificationChannelAsync(req, opts),
                () => "/v2/getnotificationchannel",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.NotificationUserChannel> GetNotificationCategoryAsync(Mezon.Protobuf.Api.DefaultNotificationCategory body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.GetNotificationCategoryAsync(req, opts),
                () => "/v2/getnotificationcategory",
                bucket);
        }

        public async Task SetNotificationCategorySettingAsync(Mezon.Protobuf.Api.SetNotificationRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SetNotificationCategorySettingAsync(req, opts),
                () => "/v2/notificationucategory/set",
                bucket);
        }

        public async Task DeleteNotificationCategorySettingAsync(Mezon.Protobuf.Api.DefaultNotificationCategory body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.DeleteNotificationCategorySettingAsync(req, opts),
                () => "/v2/notificationusercategory/delete",
                bucket);
        }

        public async Task DeleteNotificationChannelAsync(Mezon.Protobuf.Api.NotificationChannel body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.DeleteNotificationChannelAsync(req, opts),
                () => "/v2/notificationuserchannel/delete",
                bucket);
        }

        #endregion

        #region Inbox Messages

        public Task<Mezon.Protobuf.Api.ChannelMessage> CreateMessage2InboxAsync(Mezon.Protobuf.Api.Message2InboxRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateMessage2InboxAsync(req, opts),
                () => "/v2/pinmessage/inbox",
                bucket);
        }

        #endregion

        #region Channel Settings

        public Task<Mezon.Protobuf.Api.ChannelSettingListResponse> ListChannelSettingAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ChannelSettingListRequest();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListChannelSettingAsync(req, opts),
                () => $"/v2/channelsetting/{clanId}",
                bucket);
        }

        #endregion

        #region Username

        public Task<Mezon.Protobuf.Api.Session> UpdateUsernameAsync(Mezon.Protobuf.Api.UpdateUsernameRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateUsernameAsync(req, opts),
                () => "/v2/username",
                bucket);
        }

        #endregion

        #region Channel Private

        public async Task UpdateChannelPrivateAsync(Mezon.Protobuf.Api.ChangeChannelPrivateRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateChannelPrivateAsync(req, opts),
                () => "/v2/updatechannelprivate",
                bucket);
        }

        #endregion

        #region Channel Category

        public async Task ChangeChannelCategoryAsync(Mezon.Protobuf.Api.ChangeChannelCategoryRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.ChangeChannelCategoryAsync(req, opts),
                () => $"/v2/channel/category/{body.NewCategoryId}",
                bucket);
        }

        #endregion

        #region Emoji Recent

        public Task<Mezon.Protobuf.Api.EmojiRecentList> EmojiRecentListAsync(RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(new Empty(),
                (req, opts) => GRPCClient.Client.EmojiRecentListAsync(req, opts),
                () => "/v2/emojirecents",
                bucket);
        }

        #endregion

        #region Channel Users UC

        public Task<Mezon.Protobuf.Api.AllUsersAddChannelResponse> ListChannelUsersUCAsync(Mezon.Protobuf.Api.AllUsersAddChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.ListChannelUsersUCAsync(req, opts),
                () => "/v2/channeldesc/users",
                bucket);
        }

        #endregion

        #region Channel Canvas

        public Task<Mezon.Protobuf.Api.EditChannelCanvasResponse> EditChannelCanvasesAsync(Mezon.Protobuf.Api.EditChannelCanvasRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.EditChannelCanvasesAsync(req, opts),
                () => "/v2/canvases/editor",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.ChannelCanvasListResponse> GetChannelCanvasListAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ChannelCanvasListRequest();
            request.ChannelId = channelId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetChannelCanvasListAsync(req, opts),
                () => $"/v2/channel-canvases/{channelId}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.ChannelCanvasDetailResponse> GetChannelCanvasDetailAsync(long id, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ChannelCanvasDetailRequest();
            request.Id = id;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetChannelCanvasDetailAsync(req, opts),
                () => $"/v2/canvases/{id}",
                bucket);
        }

        public async Task DeleteChannelCanvasAsync(long canvasId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.DeleteChannelCanvasRequest();
            request.CanvasId = canvasId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteChannelCanvasAsync(req, opts),
                () => $"/v2/canvases/{canvasId}",
                bucket);
        }

        #endregion

        #region Favorite Channel

        public Task<Mezon.Protobuf.Api.ListFavoriteChannelResponse> GetListFavoriteChannelAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListFavoriteChannelRequest();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.GetListFavoriteChannelAsync(req, opts),
                () => $"/v2/channel/favorite/{clanId}",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.AddFavoriteChannelResponse> AddChannelFavoriteAsync(Mezon.Protobuf.Api.AddFavoriteChannelRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.AddChannelFavoriteAsync(req, opts),
                () => "/v2/channel/favorite",
                bucket);
        }

        public async Task RemoveChannelFavoriteAsync(long channelId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.RemoveFavoriteChannelRequest();
            request.ChannelId = channelId;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.RemoveChannelFavoriteAsync(req, opts),
                () => $"/v2/channel/favorite/{channelId}",
                bucket);
        }

        #endregion

        #region Clan Webhook

        public Task<Mezon.Protobuf.Api.GenerateClanWebhookResponse> GenerateClanWebhookAsync(Mezon.Protobuf.Api.GenerateClanWebhookRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.GenerateClanWebhookAsync(req, opts),
                () => "/v2/clanwebhooks",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.ListClanWebhookResponse> ListClanWebhookAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListClanWebhookRequest();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListClanWebhookAsync(req, opts),
                () => $"/v2/clanwebhooks/{clanId}",
                bucket);
        }

        public async Task UpdateClanWebhookByIdAsync(Mezon.Protobuf.Api.UpdateClanWebhookRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateClanWebhookByIdAsync(req, opts),
                () => $"/v2/clanwebhooks/{body.Id}",
                bucket);
        }

        public async Task DeleteClanWebhookByIdAsync(long id, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ClanWebhookRequest();
            request.Id = id;

            await SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.DeleteClanWebhookByIdAsync(req, opts),
                () => $"/v2/clanwebhooks/{id}",
                bucket);
        }

        #endregion

        #region Onboarding Step

        public Task<Mezon.Protobuf.Api.ListOnboardingStepResponse> ListOnboardingStepAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListOnboardingStepRequest();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListOnboardingStepAsync(req, opts),
                () => "/v2/onboardingsteps",
                bucket);
        }

        public async Task UpdateOnboardingStepAsync(Mezon.Protobuf.Api.UpdateOnboardingStepRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateOnboardingStepAsync(req, opts),
                () => $"/v2/onboardingsteps/{body.ClanId}",
                bucket);
        }

        #endregion

        #region Clan Unread Message Indicator

        public Task<Mezon.Protobuf.Api.ListClanUnreadMsgIndicatorResponse> ListClanUnreadMsgIndicatorAsync(long clanId, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListClanUnreadMsgIndicatorRequest();
            request.ClanId = clanId;

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListClanUnreadMsgIndicatorAsync(req, opts),
                () => $"/v2/{clanId}/indicator",
                bucket);
        }

        #endregion

        #region Quick Menu Access

        public async Task DeleteQuickMenuAccessAsync(Mezon.Protobuf.Api.QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.DeleteQuickMenuAccessAsync(req, opts),
                () => "/v2/quickmenuaccess",
                bucket);
        }

        public async Task AddQuickMenuAccessAsync(Mezon.Protobuf.Api.QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.AddQuickMenuAccessAsync(req, opts),
                () => "/v2/quickmenuaccess",
                bucket);
        }

        public async Task UpdateQuickMenuAccessAsync(Mezon.Protobuf.Api.QuickMenuAccess body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateQuickMenuAccessAsync(req, opts),
                () => "/v2/quickmenuaccess",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.QuickMenuAccessList> ListQuickMenuAccessAsync(long botId, long channelId, int? menuType = null, RequestOptions? options = null)
        {
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            var request = new Mezon.Protobuf.Api.ListQuickMenuAccessRequest();
            request.BotId = botId;
            request.ChannelId = channelId;
            if (menuType.HasValue)
            {
                request.MenuType = menuType.Value;
            }

            return SendRPCAsync(request,
                (req, opts) => GRPCClient.Client.ListQuickMenuAccessAsync(req, opts),
                () => "/v2/quickmenuaccess",
                bucket);
        }

        #endregion

        #region Follower

        public Task<Mezon.Protobuf.Api.IsFollowerResponse> IsFollowerAsync(Mezon.Protobuf.Api.IsFollowerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.IsFollowerAsync(req, opts),
                () => "/v2/follower",
                bucket);
        }

        #endregion

        #region Channel Messages

        public Task<PbRealtime.ChannelMessageAck> SendChannelMessageAsync(PbRealtime.ChannelMessageSend body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.SendChannelMessageAsync(req, opts),
                () => "/v2/message/send",
                bucket);
        }

        public async Task UpdateChannelMessageAsync(PbRealtime.ChannelMessageUpdate body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateChannelMessageAsync(req, opts),
                () => "/v2/message/update",
                bucket);
        }

        public async Task DeleteChannelMessageAsync(PbRealtime.ChannelMessageRemove body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.DeleteChannelMessageAsync(req, opts),
                () => "/v2/message/delete",
                bucket);
        }

        #endregion

        #region Mezon Meet Participant

        public async Task RemoveParticipantMezonMeetAsync(Mezon.Protobuf.Api.MeetParticipantRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.RemoveParticipantMezonMeetAsync(req, opts),
                () => "/v2/meet/participant/remove",
                bucket);
        }

        public async Task MuteParticipantMezonMeetAsync(Mezon.Protobuf.Api.MeetParticipantRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.MuteParticipantMezonMeetAsync(req, opts),
                () => "/v2/meet/participant/mute",
                bucket);
        }

        #endregion

        #region Room Channel Apps

        public Task<Mezon.Protobuf.Api.CreateRoomChannelApps> CreateRoomChannelAppsAsync(Mezon.Protobuf.Api.CreateRoomChannelApps body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateRoomChannelAppsAsync(req, opts),
                () => "/v2/channel-apps/createroom",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.GenerateHashChannelAppsResponse> GenerateHashChannelAppsAsync(Mezon.Protobuf.Api.GenerateHashChannelAppsRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.GenerateHashChannelAppsAsync(req, opts),
                () => "/v2/channel-apps/hash",
                bucket);
        }

        #endregion

        #region OAuth Client

        public Task<Mezon.Protobuf.Api.MezonOauthClient> GetMezonOauthClientAsync(Mezon.Protobuf.Api.GetMezonOauthClientRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.GetMezonOauthClientAsync(req, opts),
                () => "/v2/mznoauthclient",
                bucket);
        }

        public async Task DeleteMezonOauthClientAsync(Mezon.Protobuf.Api.MezonOauthClient body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.DeleteMezonOauthClientAsync(req, opts),
                () => "/v2/mznoauthclient",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.MezonOauthClient> UpdateMezonOauthClientAsync(Mezon.Protobuf.Api.MezonOauthClient body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateMezonOauthClientAsync(req, opts),
                () => "/v2/mznoauthclient",
                bucket);
        }

        #endregion

        #region SD Topics

        public Task<Mezon.Protobuf.Api.SdTopicList> ListSdTopicAsync(Mezon.Protobuf.Api.ListSdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.ListSdTopicAsync(req, opts),
                () => "/v2/sdmtopic",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.SdTopic> GetTopicDetailAsync(Mezon.Protobuf.Api.SdTopicDetailRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.GetTopicDetailAsync(req, opts),
                () => "/v2/sdmtopic/detail",
                bucket);
        }

        public Task<Mezon.Protobuf.Api.SdTopic> CreateSdTopicAsync(Mezon.Protobuf.Api.SdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.CreateSdTopicAsync(req, opts),
                () => "/v2/sdmtopic",
                bucket);
        }

        public async Task DeleteSdTopicAsync(Mezon.Protobuf.Api.DeleteSdTopicRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.DeleteSdTopicAsync(req, opts),
                () => "/v2/sdmtopic",
                bucket);
        }

        #endregion

        #region Interactive

        public async Task MessageButtonClickAsync(PbRealtime.MessageButtonClicked body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.MessageButtonClickAsync(req, opts),
                () => "/v2/interactive/buttonclick",
                bucket);
        }

        public async Task DropdownBoxSelectedAsync(PbRealtime.DropdownBoxSelected body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.DropdownBoxSelectedAsync(req, opts),
                () => "/v2/interactive/dropdownselect",
                bucket);
        }

        #endregion

        #region Voice State

        public async Task UpdateMezonVoiceStateAsync(PbRealtime.HandleParticipantMeetStateEvent body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateMezonVoiceStateAsync(req, opts),
                () => "/v2/voice/update",
                bucket);
        }

        #endregion

        #region Archived Thread

        public async Task ActiveArchivedThreadAsync(PbRealtime.ActiveArchivedThread body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.ActiveArchivedThreadAsync(req, opts),
                () => "/v2/thread/activearchive",
                bucket);
        }

        #endregion

        #region AI Agent

        public async Task AddAgentToChannelAsync(Mezon.Protobuf.Api.UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.AddAgentToChannelAsync(req, opts),
                () => "/v2/agent/addtochannel",
                bucket);
        }

        public async Task DisconnectAgentAsync(Mezon.Protobuf.Api.UpdateAIAgentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.DisconnectAgentAsync(req, opts),
                () => "/v2/agent/disconnect",
                bucket);
        }

        #endregion

        #region Report Message

        public async Task ReportMessageAbuseAsync(Mezon.Protobuf.Api.ReportMessageAbuseReqest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.ReportMessageAbuseAsync(req, opts),
                () => "/v2/message/report",
                bucket);
        }

        #endregion

        #region Registration

        public async Task<AuthenticationResponse> RegistrationEmailAsync(string basicAuthUsername, string basicAuthPassword, Mezon.Protobuf.Api.RegistrationEmailRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options = RequestOptions.CreateOrClone(options);
            options.IgnoreState = true;
            AddBasicAuthHeader(basicAuthUsername, basicAuthPassword, options);
            var bucket = new BucketIds();
            options.RequestHeaders.Add("Accept", new[] { "application/x-protobuf" });
            var response = PbSession.Parser.ParseFrom(await SendJsonAsync("POST", () => "/v2/account/registry", body, bucket, options: options));
            return new AuthenticationResponse
            {
                ApiUrl = response.ApiUrl,
                Created = response.Created,
                IsRemember = response.IsRemember,
                RefreshToken = response.RefreshToken,
                Token = response.Token,
                UserId = response.UserId,
            };
        }

        #endregion

        #region OAuth File Upload

        public Task<Mezon.Protobuf.Api.UploadAttachment> UploadOauthFileAsync(Mezon.Protobuf.Api.UploadAttachmentRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UploadOauthFileAsync(req, opts),
                () => "/v2/uploadoauthfile",
                bucket);
        }

        #endregion

        #region Account Update

        public async Task UpdateAccountAsync(Mezon.Protobuf.Api.UpdateAccountRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.UpdateAccountAsync(req, opts),
                () => "/v2/account",
                bucket);
        }

        #endregion

        #region Streaming Callback

        public Task<Mezon.Protobuf.Api.StreamHttpCallbackResponse> StreamingServerCallbackAsync(Mezon.Protobuf.Api.StreamHttpCallbackRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.StreamingServerCallbackAsync(req, opts),
                () => "/v2/stream/callback",
                bucket);
        }

        #endregion

        #region For Sale Items

        public Task<Mezon.Protobuf.Api.ForSaleItemList> ListForSaleItemsAsync(Mezon.Protobuf.Api.ListForSaleItemsRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            return SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.ListForSaleItemsAsync(req, opts),
                () => "/v2/forsale",
                bucket);
        }

        #endregion

        #region Clan Webhook Handler

        public async Task HandleClanWebhookAsync(Mezon.Protobuf.Api.ClanWebhookHandlerRequest body, RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            GRPCClient.SetHeader("Authorization", GetPrefixedToken(TokenType, AuthToken));
            options = RequestOptions.CreateOrClone(options);
            var bucket = new BucketIds();

            await SendRPCAsync(body,
                (req, opts) => GRPCClient.Client.HandleClanWebhookAsync(req, opts),
                () => $"/v2/clanwebhooks/{body.Token}/{body.Username}",
                bucket);
        }

        #endregion
    }
}
