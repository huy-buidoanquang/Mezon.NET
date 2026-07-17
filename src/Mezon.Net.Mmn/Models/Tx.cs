using System;
using System.Collections.Generic;
using System.Numerics;

namespace Mezon.Net.Mmn.Models
{
    public enum TxType
    {
        Transfer = 0,
        Faucet = 1,
    }

    public enum TxMetaStatus
    {
        Pending = 0,
        Confirmed = 1,
        Finalized = 2,
        Failed = 3,
    }

    public sealed class Tx
    {
        public int Type { get; set; }

        public string Sender { get; set; } = string.Empty;

        public string Recipient { get; set; } = string.Empty;

        public BigInteger Amount { get; set; }

        public ulong Timestamp { get; set; }

        public string TextData { get; set; } = string.Empty;

        public ulong Nonce { get; set; }

        public string ExtraInfo { get; set; } = string.Empty;

        public string ZkProof { get; set; } = string.Empty;

        public string ZkPub { get; set; } = string.Empty;
    }

    public sealed class SignedTx
    {
        public Tx Tx { get; set; } = new();

        public string Sig { get; set; } = string.Empty;
    }

    public sealed class UserSig
    {
        public byte[] PubKey { get; set; } = Array.Empty<byte>();

        public byte[] Sig { get; set; } = Array.Empty<byte>();
    }

    public sealed class TxMetaResponse
    {
        public string Sender { get; set; } = string.Empty;

        public string Recipient { get; set; } = string.Empty;

        public BigInteger Amount { get; set; }

        public ulong Nonce { get; set; }

        public ulong Timestamp { get; set; }

        public TxMetaStatus Status { get; set; }
    }
}
