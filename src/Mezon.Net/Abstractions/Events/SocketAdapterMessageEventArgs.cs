using System;
using System.Net.WebSockets;
using System.Text.Json.Nodes;

namespace Mezon.NET.Abstractions.Events
{
    public class SocketAdapterMessageEventArgs : EventArgs
    {
        public JsonNode? MessageNode { get; }
        public byte[]? DecodedPartyData { get; }
        public WebSocket? Socket { get; }

        public SocketAdapterMessageEventArgs(JsonNode? messageNode, byte[]? decodedPartyData, WebSocket? socket)
        {
            MessageNode = messageNode;
            DecodedPartyData = decodedPartyData;
            Socket = socket;
        }
    }
}
