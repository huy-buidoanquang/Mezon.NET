using Grpc.Net.Client;
using Mmn;
using Mezon.Net.Mmn.Models;
using Mezon.Net.Mmn.Utils;
using AddTxResponse = Mezon.Net.Mmn.Models.AddTxResponse;
using TxInfo = Mezon.Net.Mmn.Models.TxInfo;

namespace Mezon.Net.Mmn
{
    public sealed class MmnNodeClient : IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly HealthService.HealthServiceClient _healthClient;
        private readonly TxService.TxServiceClient _txClient;
        private readonly AccountService.AccountServiceClient _accClient;
        private bool _disposed;

        public MmnNodeClient(string endpoint)
        {
            _channel = GrpcChannel.ForAddress(endpoint);
            _healthClient = new HealthService.HealthServiceClient(_channel);
            _txClient = new TxService.TxServiceClient(_channel);
            _accClient = new AccountService.AccountServiceClient(_channel);
        }

        public async Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return await _healthClient.CheckAsync(new Empty(), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<AddTxResponse> AddTxAsync(SignedTx tx, CancellationToken cancellationToken = default)
        {
            var signedTxMsg = ProtoConverter.ToProtoSigTx(tx);
            var response = await _txClient.AddTxAsync(signedTxMsg, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!response.Ok)
            {
                throw new InvalidOperationException($"Add transaction failed: {response.Error}");
            }

            return new AddTxResponse
            {
                Ok = response.Ok,
                TxHash = response.TxHash,
                Error = response.Error,
            };
        }

        public async Task<Account> GetAccountAsync(string address, CancellationToken cancellationToken = default)
        {
            var response = await _accClient.GetAccountAsync(
                new GetAccountRequest { Address = address },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return ProtoConverter.FromProtoAccount(response);
        }

        public async Task<TxHistoryResponse> GetTxHistoryAsync(
            string address,
            int limit,
            int offset,
            int filter,
            CancellationToken cancellationToken = default)
        {
            var response = await _accClient.GetTxHistoryAsync(
                new GetTxHistoryRequest
                {
                    Address = address,
                    Limit = (uint)limit,
                    Offset = (uint)offset,
                    Filter = (uint)filter,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return ProtoConverter.FromProtoTxHistory(response);
        }

        public Task<TxService.TxServiceClient> SubscribeTransactionStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_txClient);

        public async Task<TxInfo> GetTxByHashAsync(string txHash, CancellationToken cancellationToken = default)
        {
            var response = await _txClient.GetTxByHashAsync(
                new GetTxByHashRequest { TxHash = txHash },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(response.Error))
            {
                throw new InvalidOperationException($"Get transaction by hash failed: {response.Error}");
            }

            return new TxInfo
            {
                Sender = response.Tx.Sender,
                Recipient = response.Tx.Recipient,
                Amount = ProtoConverter.Uint256FromString(response.Tx.Amount),
                Timestamp = (long)response.Tx.Timestamp,
                TextData = response.Tx.TextData,
                Nonce = response.Tx.Nonce,
                Status = response.Tx.Status.ToString(),
                ErrMsg = response.Tx.ErrMsg,
                ExtraInfo = response.Tx.ExtraInfo,
            };
        }

        public async Task<ulong> GetCurrentNonceAsync(string address, string tag, CancellationToken cancellationToken = default)
        {
            var response = await _accClient.GetCurrentNonceAsync(
                new GetCurrentNonceRequest { Address = address, Tag = tag },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(response.Error))
            {
                throw new InvalidOperationException($"Get current nonce failed: {response.Error}");
            }

            return response.Nonce;
        }

        public GrpcChannel Channel => _channel;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _channel.Dispose();
            _disposed = true;
        }
    }
}
