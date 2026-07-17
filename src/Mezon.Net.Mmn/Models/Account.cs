using System.Numerics;

namespace Mezon.Net.Mmn.Models
{
    public sealed class Account
    {
        public string Address { get; set; } = string.Empty;

        public BigInteger Balance { get; set; }

        public ulong Nonce { get; set; }
    }
}
