using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using MezonRPC = Mezon.Protobuf.Service.Mezon;

namespace Mezon.NET.Abstractions
{
    public delegate IGRPCClient GRPCClientProvider(string baseUrl);

    public interface IGRPCClient : IDisposable
    {
        /// <summary>
        ///     Gets the underlying gRPC client.
        /// </summary>
        MezonRPC.MezonClient Client { get; }

        /// <summary>
        ///     Sets the gRPC header of this client for all requests.
        /// </summary>
        /// <param name="key">The field name of the header.</param>
        /// <param name="value">The value of the header.</param>
        void SetHeader(string key, string value);

        /// <summary>
        ///     Sets the cancellation token for this client.
        /// </summary>
        /// <param name="cancelToken">The cancellation token.</param>
        void SetCancelToken(CancellationToken cancelToken);

        /// <summary>
        ///     Gets the gRPC call options with headers and cancellation token.
        /// </summary>
        /// <returns>The call options to use for gRPC calls.</returns>
        CallOptions GetCallOptions();

        /// <summary>
        ///     Gets the gRPC metadata (headers) for the current client.
        /// </summary>
        /// <returns>The metadata containing all headers.</returns>
        Metadata GetMetadata();

        /// <summary>
        ///     Gets the current cancellation token.
        /// </summary>
        /// <returns>The cancellation token.</returns>
        CancellationToken GetCancellationToken();

        /// <summary>
        ///     Sends a gRPC request using the provided async method delegate.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request message.</typeparam>
        /// <typeparam name="TResponse">The type of the response message.</typeparam>
        /// <param name="request">The request message to send.</param>
        /// <param name="methodCall">The gRPC method delegate to invoke.</param>
        /// <param name="cancellationToken">Optional cancellation token to override the default.</param>
        /// <returns>The response from the gRPC call.</returns>
        Task<AsyncUnaryCall<TResponse>> SendAsync<TRequest, TResponse>(
            TRequest request,
            Func<TRequest, CallOptions, AsyncUnaryCall<TResponse>> methodCall,
            CancellationToken? cancellationToken = null)
            where TRequest : class
            where TResponse : class;
    }
}
