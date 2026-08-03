namespace Mezon.Net.Mmn.Models
{
    public sealed class MmnConfig
    {
        public string Endpoint { get; set; } = string.Empty;

        public string ZkProveEndpoint { get; set; } = string.Empty;

        public int TimeoutMs { get; set; } = 7000;
    }
}
