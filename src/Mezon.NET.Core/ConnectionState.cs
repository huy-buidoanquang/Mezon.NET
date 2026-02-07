namespace Mezon.NET.Core
{
    /// <summary> Specifies the connection state of a client. </summary>
    public enum ConnectionState : byte
    {
        /// <summary> The client has disconnected from Mezon. </summary>
        Disconnected,
        /// <summary> The client is connecting to Mezon. </summary>
        Connecting,
        /// <summary> The client has established a connection to Mezon. </summary>
        Connected,
        /// <summary> The client is disconnecting from Mezon. </summary>
        Disconnecting
    }
}
