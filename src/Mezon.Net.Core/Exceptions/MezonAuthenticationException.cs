using System;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     The exception that is thrown when authentication or session operations fail.
    /// </summary>
    public class MezonAuthenticationException : MezonException
    {
        public MezonAuthenticationException(string message) : base(message)
        {
        }

        public MezonAuthenticationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
