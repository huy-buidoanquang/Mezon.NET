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

namespace Mezon.NET.Api
{
    internal class MezonApiClient : IMezonApiClient, IDisposable, IAsyncDisposable
    {
        private static readonly ConcurrentDictionary<string, Func<BucketIds, BucketId>> _bucketIdGenerators = new ConcurrentDictionary<string, Func<BucketIds, BucketId>>();

        public event Func<string, string, double, Task> SentRequest { add { _sentRequestEvent.Add(value); } remove { _sentRequestEvent.Remove(value); } }
        private readonly AsyncEvent<Func<string, string, double, Task>> _sentRequestEvent = new AsyncEvent<Func<string, string, double, Task>>();

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
            await _sentRequestEvent.InvokeAsync(method, endpoint, milliseconds).ConfigureAwait(false);

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
            await _sentRequestEvent.InvokeAsync("POST", endpoint, milliseconds).ConfigureAwait(false);

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
