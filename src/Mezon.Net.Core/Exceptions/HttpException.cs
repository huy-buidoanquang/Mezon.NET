using System.Net;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     The exception that is thrown when an HTTP bootstrap request fails
    ///     (initial authentication before the socket session is established).
    /// </summary>
    public class HttpException : MezonException
    {
        public HttpStatusCode HttpCode { get; }
        public string? Reason { get; }
        public IRequest Request { get; }

        public HttpException(HttpStatusCode httpCode, IRequest request, string? reason = null)
            : base(CreateMessage(httpCode, reason))
        {
            HttpCode = httpCode;
            Request = request;
            Reason = reason;
        }

        private static string CreateMessage(HttpStatusCode httpCode, string? reason)
        {
            return reason != null
                ? $"HTTP {(int)httpCode} {httpCode}: {reason}"
                : $"HTTP {(int)httpCode} {httpCode}";
        }
    }
}
