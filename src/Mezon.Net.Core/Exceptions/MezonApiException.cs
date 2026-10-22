using System;
using System.Text;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     The exception that is thrown when a Mezon socket API request fails.
    /// </summary>
    public class MezonApiException : MezonException
    {
        public MezonStatusCode StatusCode { get; }

        /// <summary>The code exactly as the server sent it. Codes outside <see cref="MezonStatusCode"/>, such as
        /// mezon-proto-server's 400, 403, 500 and 503, leave <see cref="StatusCode"/> at Unknown.</summary>
        public int RawCode { get; private set; }

        public string? ApiName { get; }
        public string? Detail { get; }

        public MezonApiException(MezonStatusCode statusCode, string? apiName = null, string? detail = null)
            : base(BuildMessage(statusCode, apiName, detail))
        {
            StatusCode = statusCode;
            RawCode = (int)statusCode;
            ApiName = apiName;
            Detail = detail;
        }

        public static MezonApiException FromSocketResponse(int code, string? apiName, ReadOnlyMemory<byte> payload)
        {
            var statusCode = Enum.IsDefined(typeof(MezonStatusCode), code)
                ? (MezonStatusCode)code
                : MezonStatusCode.Unknown;

            return new MezonApiException(statusCode, apiName, TryDecodePayload(payload)) { RawCode = code };
        }

        private static string? TryDecodePayload(ReadOnlyMemory<byte> payload)
        {
            if (payload.Length == 0)
            {
                return null;
            }

            try
            {
                var text = Encoding.UTF8.GetString(payload.Span).Trim();
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch
            {
                return null;
            }
        }

        private static string BuildMessage(MezonStatusCode statusCode, string? apiName, string? detail)
        {
            var prefix = string.IsNullOrEmpty(apiName)
                ? $"Socket API failed with {statusCode}"
                : $"Socket API '{apiName}' failed with {statusCode}";

            return string.IsNullOrEmpty(detail) ? prefix : $"{prefix}: {detail}";
        }
    }
}
