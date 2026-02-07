using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents an RPC call to execute a Lua function on the server.
    /// </summary>
    public class RPCSend : SocketSendBase
    {
        [JsonPropertyName("rpc")]
        public RPCSendDetails RPCSendDetails { get; set; }
    }

    /// <summary>
    /// Contains the details for executing a Lua function on the server.
    /// </summary>
    public class RPCSendDetails
    {
        /// <summary>
        /// The authentication key used when executed as a non-client HTTP request.
        /// </summary>
        [JsonPropertyName("http_key")]
        public string HttpKey { get; set; }

        /// <summary>
        /// The identifier of the function.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// The payload of the function, which must be a JSON object string.
        /// </summary>
        [JsonPropertyName("payload")]
        public string Payload { get; set; }
    }
}
