using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Mezon.Net.Abstractions
{
    public struct HttpResponse
    {
        public HttpStatusCode StatusCode { get; }
        public Dictionary<string, string> Headers { get; }
        public Stream Stream { get; }

        public HttpResponse(HttpStatusCode statusCode, Dictionary<string, string> headers, Stream stream)
        {
            StatusCode = statusCode;
            Headers = headers;
            Stream = stream;
        }
    }
}
