namespace Mezon.Net.Mmn.Models
{
    public sealed class AddTxResponse
    {
        public bool Ok { get; set; }

        public string TxHash { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;
    }
}
