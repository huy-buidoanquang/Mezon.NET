using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Mezon.Net.Core;
using Mezon.Net.Abstractions;
using MezonRPC = Mezon.Protobuf.Service.Mezon;

namespace Mezon.Net.Api
{
    internal sealed class DefaultGRPCClient : IGRPCClient, IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly MezonRPC.MezonClient _client;
        private readonly Dictionary<string, string> _headers;
        private readonly object _lock = new object();
        private CancellationToken _cancelToken;
        private bool _isDisposed;

        public DefaultGRPCClient(string url, bool useProxy = false, IWebProxy? webProxy = null)
        {
            Check.NotNullOrWhitespace(url, nameof(url));

            _headers = new Dictionary<string, string>();
            _cancelToken = CancellationToken.None;

            var httpClientHandler = new HttpClientHandler();
            if (useProxy && webProxy != null)
            {
                httpClientHandler.Proxy = webProxy;
                httpClientHandler.UseProxy = true;
            }

            var httpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, httpClientHandler);
            _channel = GrpcChannel.ForAddress(url, new GrpcChannelOptions
            {
                HttpHandler = httpHandler,
                MaxReceiveMessageSize = null,
                MaxSendMessageSize = null
            });

            _client = new MezonRPC.MezonClient(_channel);
        }

        public MezonRPC.MezonClient Client
        {
            get
            {
                ThrowIfDisposed();
                return _client;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _channel?.Dispose();
            _isDisposed = true;
        }

        public void SetCancelToken(CancellationToken cancelToken)
        {
            ThrowIfDisposed();
            _cancelToken = cancelToken;
        }

        public void SetHeader(string key, string value)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            lock (_lock)
            {
                _headers[key] = value ?? string.Empty;
            }
        }

        public CallOptions GetCallOptions()
        {
            ThrowIfDisposed();

            var metadata = new Metadata();

            lock (_lock)
            {
                foreach (var header in _headers)
                {
                    metadata.Add(header.Key, header.Value);
                }
            }

            return new CallOptions(
                headers: metadata,
                cancellationToken: _cancelToken
            );
        }

        public Metadata GetMetadata()
        {
            ThrowIfDisposed();

            var metadata = new Metadata();

            lock (_lock)
            {
                foreach (var header in _headers)
                {
                    metadata.Add(header.Key, header.Value);
                }
            }

            return metadata;
        }

        public CancellationToken GetCancellationToken()
        {
            ThrowIfDisposed();
            return _cancelToken;
        }

        /// <summary>
        ///     Sends a gRPC request using the provided async method delegate.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request message.</typeparam>
        /// <typeparam name="TResponse">The type of the response message.</typeparam>
        /// <param name="request">The request message to send.</param>
        /// <param name="methodCall">The gRPC method delegate to invoke.</param>
        /// <param name="cancellationToken">Optional cancellation token to override the default.</param>
        /// <returns>The response from the gRPC call.</returns>
        public async Task<AsyncUnaryCall<TResponse>> SendAsync<TRequest, TResponse>(
            TRequest request,
            Func<TRequest, CallOptions, AsyncUnaryCall<TResponse>> methodCall,
            CancellationToken? cancellationToken = null)
            where TRequest : class
            where TResponse : class
        {
            ThrowIfDisposed();
            Check.NotNull(request, nameof(request));
            Check.NotNull(methodCall, nameof(methodCall));

            var callOptions = GetCallOptions();

            if (cancellationToken.HasValue)
            {
                callOptions = callOptions.WithCancellationToken(cancellationToken.Value);
            }

            return methodCall(request, callOptions);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(DefaultGRPCClient));
            }
        }
    }
}
