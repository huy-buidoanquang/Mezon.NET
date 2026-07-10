namespace Mezon.Net.Mmn.Models
{
    public sealed class EphemeralKeyPair
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
    }

    public sealed class ZkProofResult
    {
        public string Proof { get; set; } = string.Empty;
        public string PublicInput { get; set; } = string.Empty;
    }

    public sealed class NonceResult
    {
        public long Nonce { get; set; }
    }

    public sealed class SendTransactionResult
    {
        public bool Ok { get; set; }
        public string? TxHash { get; set; }
        public string? Error { get; set; }
    }

    public sealed class SendTransactionRequest
    {
        public string Sender { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public long Nonce { get; set; }
        public string? TextData { get; set; }
        public object? ExtraInfo { get; set; }
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string ZkProof { get; set; } = string.Empty;
        public string ZkPub { get; set; } = string.Empty;
    }

    public sealed class ZkProofRequest
    {
        public long UserId { get; set; } = 0;
        public string Jwt { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string EphemeralPublicKey { get; set; } = string.Empty;
    }
}
