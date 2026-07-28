using System;

namespace Mezon.Net.Core
{
    public class NetworkTransportException : MezonException
    {
        public NetworkTransportException(string message) : base(message)
        {
        }

        public NetworkTransportException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class NetworkTransportTimeoutException : NetworkTransportException
    {
        public NetworkTransportTimeoutException(string message) : base(message)
        {
        }

        public NetworkTransportTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class NetworkTransportUnauthorizationException : NetworkTransportException
    {
        public NetworkTransportUnauthorizationException(string? message = null) : base(message ?? "Unauthorized.")
        {
        }
    }

    /// <summary>
    /// Thrown when an outbound abridged frame exceeds the client send size limit.
    /// Does not indicate a broken connection — only that send was rejected.
    /// </summary>
    public class NetworkTransportPayloadTooLargeException : NetworkTransportException
    {
        public int FrameSize { get; }
        public int MaxFrameSize { get; }

        public NetworkTransportPayloadTooLargeException(int frameSize, int maxFrameSize)
            : base($"Abridged frame size {frameSize} exceeds client send limit {maxFrameSize}.")
        {
            FrameSize = frameSize;
            MaxFrameSize = maxFrameSize;
        }
    }
}
