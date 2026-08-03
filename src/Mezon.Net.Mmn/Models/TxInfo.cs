using System.Numerics;
using System.Text.Json;

namespace Mezon.Net.Mmn.Models
{
    public sealed class TxInfo
    {
        public string Sender { get; set; } = string.Empty;

        public string Recipient { get; set; } = string.Empty;

        public BigInteger Amount { get; set; }

        public long Timestamp { get; set; }

        public string TextData { get; set; } = string.Empty;

        public ulong Nonce { get; set; }

        public string Status { get; set; } = string.Empty;

        public string ErrMsg { get; set; } = string.Empty;

        public string ExtraInfo { get; set; } = string.Empty;

        public Dictionary<string, string>? DeserializedExtraInfo()
        {
            if (string.IsNullOrEmpty(ExtraInfo))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(ExtraInfo);
            }
            catch
            {
                return null;
            }
        }
    }
}
