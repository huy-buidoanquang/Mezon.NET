using System;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     Base exception for operational errors raised by the Mezon.Net library.
    /// </summary>
    public abstract class MezonException : Exception
    {
        protected MezonException(string message) : base(message)
        {
        }

        protected MezonException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
