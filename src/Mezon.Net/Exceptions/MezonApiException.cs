using System;
using System.Net;

namespace Mezon.NET.Exceptions
{
    /// <summary>
    /// An exception thrown for receiving a non-successful HTTP status code.
    /// </summary>
    public class MezonApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public MezonApiException(string message, HttpStatusCode statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
