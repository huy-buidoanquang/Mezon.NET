namespace Mezon.Net.Mmn.Models
{
    public sealed class TxHistoryResponse
    {
        public uint Total { get; set; }

        public List<TxMetaResponse> Txs { get; set; } = new();
    }
}
