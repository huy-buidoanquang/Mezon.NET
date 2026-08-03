#if !NETSTANDARD2_1
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Mmn;
using Mezon.Net.Mmn.Models;
using Mezon.Net.Mmn.Utils;

namespace Mezon.Net.Sdk
{
    public sealed partial class MezonClient
    {
        private Task? _mmnInitTask;
        private MmnClient? _mmnClient;

        public KeyPairAccount? KeyGen { get; private set; }

        public string? AddressMmn { get; private set; }

        public ZkProofData? ZkProofs { get; private set; }

        public MmnClient? Mmn => _mmnClient;

        public string GetAddress(long userId) => CryptoHelper.GenerateAddress(userId.ToString());

        public Task<ProveResponse> GetZkProofsAsync(
            string userId,
            string address,
            string ephemeralPublicKey,
            string jwt,
            CancellationToken cancellationToken = default)
        {
            EnsureMmnClient();
            return _mmnClient!.ZkProveClient.GenerateZkProofAsync(
                userId,
                address,
                ephemeralPublicKey,
                jwt,
                cancellationToken);
        }

        public async Task<ulong> GetCurrentNonceAsync(long userId, string tag = "pending", CancellationToken cancellationToken = default)
        {
            EnsureMmnClient();
            var address = GetAddress(userId);
            return await _mmnClient!.NodeClient.GetCurrentNonceAsync(address, tag, cancellationToken).ConfigureAwait(false);
        }

        public async Task<AddTxResponse> SendTransferAsync(
            string recipient,
            BigInteger amount,
            string? textData = null,
            Dictionary<string, string>? extraInfo = null,
            CancellationToken cancellationToken = default)
        {
            EnsureMmnClient();
            if (KeyGen == null || AddressMmn == null || ZkProofs == null)
            {
                throw new InvalidOperationException("MMN account is not initialized. Ensure MMNApiUrl and ZkApiUrl are configured and login completed.");
            }

            var account = await _mmnClient!.NodeClient.GetAccountAsync(AddressMmn, cancellationToken).ConfigureAwait(false);
            var nonce = account.Nonce + 1;
            var unsigned = CryptoHelper.BuildTransferTx(
                (int)TxType.Transfer,
                AddressMmn,
                recipient,
                amount,
                nonce,
                (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                textData ?? string.Empty,
                extraInfo,
                ZkProofs.Proof,
                ZkProofs.PublicInput);

            var publicKeyBytes = CryptoHelper.Base58Decode(KeyGen.PublicKey);
            var signed = CryptoHelper.SignTx(unsigned, publicKeyBytes, KeyGen.PrivateKey);
            return await _mmnClient.NodeClient.AddTxAsync(signed, cancellationToken).ConfigureAwait(false);
        }

        private async Task InitializeMmnAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(Options.MMNApiUrl))
            {
                return;
            }

            if (KeyGen != null && AddressMmn != null && ZkProofs != null)
            {
                return;
            }

            if (_mmnInitTask != null)
            {
                await _mmnInitTask.ConfigureAwait(false);
                return;
            }

            _mmnInitTask = InitializeMmnCoreAsync(cancellationToken);
            await _mmnInitTask.ConfigureAwait(false);
        }

        private async Task InitializeMmnCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                EnsureMmnClient();
                KeyGen ??= CryptoHelper.GenerateKeyPairAccount();
                AddressMmn ??= CryptoHelper.GenerateAddress(Options.BotId.ToString());

                var session = _engine.CurrentSession;
                var idToken = session.IdToken;
                if (!string.IsNullOrEmpty(idToken) && !string.IsNullOrEmpty(Options.ZkApiUrl))
                {
                    var proofResponse = await _mmnClient!.ZkProveClient.GenerateZkProofAsync(
                        Options.BotId.ToString(),
                        AddressMmn,
                        KeyGen.PublicKey,
                        idToken,
                        cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(proofResponse.Error))
                    {
                        throw new InvalidOperationException($"Failed to generate ZK proof: {proofResponse.Error}");
                    }

                    if (proofResponse.Data == null)
                    {
                        throw new InvalidOperationException("ZK proof response data was empty.");
                    }

                    ZkProofs ??= new ZkProofData
                    {
                        Proof = proofResponse.Data.Proof ?? string.Empty,
                        PublicInput = proofResponse.Data.PublicInput ?? string.Empty,
                    };
                }
            }
            catch
            {
                _mmnInitTask = null;
                throw;
            }
        }

        private void EnsureMmnClient()
            => _mmnClient ??= new MmnClient(new MmnConfig
            {
                Endpoint = Options.MMNApiUrl,
                ZkProveEndpoint = Options.ZkApiUrl,
                TimeoutMs = Options.ApiTimeoutInMilliseconds,
            });

        partial void DisposeMmn() => _mmnClient?.Dispose();
    }
}
#else
using System.Threading;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk
{
    public sealed partial class MezonClient
    {
        private Task InitializeMmnAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        partial void DisposeMmn()
        {
        }
    }
}
#endif
