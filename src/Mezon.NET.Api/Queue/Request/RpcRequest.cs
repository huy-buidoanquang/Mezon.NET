using System;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using Mezon.NET.Core;
using Mezon.NET.Abstractions;

namespace Mezon.NET.Queue
{
    public class RpcRequest<TRequest, TResponse> : IRequest
        where TRequest : class
        where TResponse : class
    {
        public IGRPCClient GRPCClient { get; }
        public string Endpoint { get; set; }
        public TRequest Payload { get; set; }
        public Func<TRequest, CallOptions, AsyncUnaryCall<TResponse>> MethodCall { get; set; }
        public DateTimeOffset? TimeoutAt { get; }
        public TaskCompletionSource<Stream> Promise { get; }
        public RequestOptions Options { get; }

        public RpcRequest(IGRPCClient grpcClient, string endpoint, TRequest payload, Func<TRequest, CallOptions, AsyncUnaryCall<TResponse>> methodCall, RequestOptions options)
        {
            Check.NotNull(options, nameof(options));

            GRPCClient = grpcClient;
            Endpoint = endpoint;
            Payload = payload;
            MethodCall = methodCall;
            Options = options;
            TimeoutAt = options.ApiSendTimeout.HasValue ? DateTimeOffset.UtcNow.AddMilliseconds(options.ApiSendTimeout.Value) : (DateTimeOffset?)null;
            Promise = new TaskCompletionSource<Stream>();
        }

        public Task<AsyncUnaryCall<TResponse>> SendRPCAsync() => GRPCClient.SendAsync(Payload, MethodCall);
    }
}
