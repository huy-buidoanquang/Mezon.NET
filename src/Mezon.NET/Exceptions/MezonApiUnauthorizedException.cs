using System.Net;

namespace Mezon.NET.Exceptions
{
    class MezonApiUnauthorizedException : MezonApiException
    {
        public MezonApiUnauthorizedException(string message) : base(message, HttpStatusCode.Unauthorized)
        {
        }
    }
}
