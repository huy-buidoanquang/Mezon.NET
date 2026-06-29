using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    public class SocketSendBase
    {
        [JsonPropertyName("cid")]
        public string CID { get; }
    }
}
