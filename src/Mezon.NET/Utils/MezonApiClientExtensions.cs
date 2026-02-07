using System.Collections.Generic;
using System.Net.Http;

namespace Mezon.NET.Utils
{
    public static class MezonApiClientExtensions
    {
        public static void BuildHttpHeader(this HttpRequestMessage httpRequestMessage, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            Check.NotNull(httpRequestMessage, nameof(httpRequestMessage));
            if (headers is null)
            {
                return;
            }

            foreach (var header in headers)
            {
                httpRequestMessage.Headers.Add(header.Key, header.Value);
            }
        }
    }
}
