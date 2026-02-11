using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Core;
using Mezon.NET.Abstractions;

namespace Mezon.NET.Queue
{
    public struct WebSocketRequest : IRequest
    {
        public IWebSocketClient Client { get; }
        public ReadOnlyMemory<byte> Data { get; }
        public bool IgnoreLimit { get; }
        public DateTimeOffset? TimeoutAt { get; }
        public TaskCompletionSource<Stream> Promise { get; }
        public RequestOptions Options { get; }
        public CancellationToken CancelToken { get; internal set; }

        public WebSocketRequest(IWebSocketClient client, ReadOnlyMemory<byte> data, bool ignoreLimit, RequestOptions options)
        {
            Check.NotNull(options, nameof(options));

            Client = client;
            Data = data;
            IgnoreLimit = ignoreLimit;
            Options = options;
            TimeoutAt = options.Timeout.HasValue ? DateTimeOffset.UtcNow.AddMilliseconds(options.Timeout.Value) : (DateTimeOffset?)null;
            CancelToken = options.CancelToken;
            Promise = new TaskCompletionSource<Stream>();
        }

        public ValueTask SendAsync() => Client.SendAsync(Data);
    }
}
