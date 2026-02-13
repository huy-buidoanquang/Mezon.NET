using System;

namespace Mezon.NET.Core
{
    public class SessionRefreshFailedException : Exception
    {
        public SessionRefreshFailedException() : base("Session refresh failed.") { }
        public SessionRefreshFailedException(string message) : base(message) { }
    }
}
