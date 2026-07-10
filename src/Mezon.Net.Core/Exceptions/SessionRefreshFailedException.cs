using System;

namespace Mezon.Net.Core
{
    public class SessionRefreshFailedException : MezonException
    {
        public SessionRefreshFailedException() : base("Session refresh failed.") { }
        public SessionRefreshFailedException(string message) : base(message) { }
        public SessionRefreshFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
