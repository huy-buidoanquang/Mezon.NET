using System;

namespace Mezon.Net.Core.Exceptions
{
    public class NetworkTransportException : Exception
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
}
