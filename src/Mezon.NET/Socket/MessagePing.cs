using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents an application-level heartbeat ping.
    /// </summary>
    public class MessagePing : SocketSendBase
    {
        [JsonPropertyName("ping")]
        public object Ping { get; set; } = new object();
    }
}
