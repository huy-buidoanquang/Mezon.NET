using System;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     The exception that is thrown when a socket or realtime operation requires a connection that is not available.
    /// </summary>
    public class MezonConnectionException : MezonException
    {
        public MezonConnectionException(string message) : base(message)
        {
        }

        public MezonConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
