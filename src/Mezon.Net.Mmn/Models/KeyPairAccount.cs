namespace Mezon.Net.Mmn.Models
{
    public sealed class KeyPairAccount
    {
        public KeyPairAccount(string publicKey, byte[] privateKey)
        {
            PublicKey = publicKey;
            PrivateKey = privateKey;
        }

        public string PublicKey { get; }

        public byte[] PrivateKey { get; }
    }
}
