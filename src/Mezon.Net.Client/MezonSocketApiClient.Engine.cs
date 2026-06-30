using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Mezon.Net.Api;
using Mezon.Net.Core;
using Mezon.Net.Utils;
using Mezon.Net.Core.Protocol;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Client
{
    internal partial class MezonSocketApiClient
    {
        private readonly SocketRequestHub _requestHub = new();
        private long _lastPingSentMs;

        public int LatencyMilliseconds { get; private set; }

        public Task<TResponse> SendApiAsync<TRequest, TResponse>(
            string apiName,
            TRequest request,
            MessageParser<TResponse> responseParser,
            RequestOptions? options = null)
            where TRequest : IMessage<TRequest>
            where TResponse : IMessage<TResponse>
        {
            ApiNameIndexMap.TryGetIndex(apiName, out var apiIndex);
            var envelope = new Envelope
            {
                ApiRequestEvent = new ApiRequestEvent
                {
                    ApiIndex = apiIndex,
                    ApiName = apiName,
                    Body = request.ToByteString(),
                }
            };

            return SendApiEnvelopeAsync(envelope, responseParser, options);
        }

        public async Task<TResponse> SendApiEnvelopeAsync<TResponse>(
            Envelope envelope,
            MessageParser<TResponse> responseParser,
            RequestOptions? options = null)
            where TResponse : IMessage<TResponse>
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            var cid = _requestHub.AllocateCid();
            envelope.Cid = cid;
            var timeout = options.SocketSendTimeout ?? SocketRequestHub.DefaultTimeoutMilliseconds;
            var waitTask = _requestHub.WaitAsync(cid, timeout, options.CancelToken);
            await WebSocketClient.SendAsync(MezonMessageType.Abridged, cid, envelope.ToByteArray()).ConfigureAwait(false);
            var socketResponse = await waitTask.ConfigureAwait(false);

            if (socketResponse.Code != 0)
            {
                throw new RPCException(new Grpc.Core.Status((StatusCode)socketResponse.Code, $"Socket API failed with code {socketResponse.Code}"));
            }

            return responseParser.ParseFrom(socketResponse.Payload.Span);
        }

        public async Task<Envelope> SendEnvelopeAsync(Envelope envelope, RequestOptions? options = null)
        {
            options ??= RequestOptions.CreateOrClone(options);
            CheckState();

            var cid = _requestHub.AllocateCid();
            envelope.Cid = cid;
            var timeout = options.SocketSendTimeout ?? SocketRequestHub.DefaultTimeoutMilliseconds;
            var waitTask = _requestHub.WaitAsync(cid, timeout, options.CancelToken);
            await WebSocketClient.SendAsync(MezonMessageType.Abridged, cid, envelope.ToByteArray()).ConfigureAwait(false);
            var socketResponse = await waitTask.ConfigureAwait(false);

            if (socketResponse.Payload.Length > 0)
            {
                return Envelope.Parser.ParseFrom(socketResponse.Payload.Span);
            }

            return envelope;
        }

        private void OnSocketMessageReceived(MezonMessageType type, int cid, int code, ReadOnlyMemory<byte> data, Envelope? envelope)
        {
            if (type == MezonMessageType.Heartbeat)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (_lastPingSentMs > 0)
                {
                    LatencyMilliseconds = (int)Math.Max(0, now - _lastPingSentMs);
                }

                _requestHub.TryComplete(cid, code, ReadOnlyMemory<byte>.Empty);
                return;
            }

            if (type == MezonMessageType.Api)
            {
                _requestHub.TryComplete(cid, code, data);
                return;
            }

            if (envelope != null && envelope.Cid > 0)
            {
                _requestHub.TryComplete(envelope.Cid, code, envelope.ToByteArray());
            }
        }

        public override Task<ClanDescList> ListClanDescsAsync(PaginationParams args, RequestOptions? options = null)
        {
            var request = new ListClanDescRequest
            {
                Limit = args.Limit.GetValueOrDefault(50),
                State = args.State.GetValueOrDefault(0),
                Cursor = args.Cursor.GetValueOrDefault(string.Empty),
            };
            return SendApiAsync("ListClanDescs", request, ClanDescList.Parser, options);
        }

        public override async Task<AuthenticationResponse> RefreshSessionAsync(
            string basicAuthUsername,
            string basicAuthPassword,
            Mezon.Net.Api.SessionRefreshRequest body,
            RequestOptions? options = null)
        {
            Check.NotNull(body, nameof(body));
            options = RequestOptions.CreateOrClone(options);
            var request = new Internal.Api.SessionRefreshRequest
            {
                IsRemember = body.IsRemember ?? false,
                Token = body.Token,
            };
            if (body.Vars != null)
            {
                foreach (var pair in body.Vars)
                {
                    request.Vars[pair.Key] = pair.Value;
                }
            }

            var session = await SendApiAsync("SessionRefresh", request, global::Mezon.Net.Internal.Api.Session.Parser, options).ConfigureAwait(false);
            return new AuthenticationResponse
            {
                ApiUrl = session.ApiUrl,
                Created = session.Created,
                IsRemember = session.IsRemember,
                RefreshToken = session.RefreshToken,
                Token = session.Token,
                UserId = session.UserId,
            };
        }
    }
}
