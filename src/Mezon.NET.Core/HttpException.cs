using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     The exception that is thrown if an error occurs while processing an Mezon HTTP request.
    /// </summary>
    public class HttpException : Exception
    {
        /// <summary>
        ///     Gets the HTTP status code returned by Mezon.
        /// </summary>
        /// <returns>
        ///     An HTTP status code from Mezon.
        /// </returns>
        public HttpStatusCode HttpCode { get; }
        /// <summary>
        ///     Gets the JSON error code returned by Mezon.
        /// </summary>
        /// <returns>
        ///     A JSON error code from Mezon, or <see langword="null" /> if none.
        /// </returns>
        public MezonErrorCode MezonCode { get; }
        /// <summary>
        ///     Gets the reason of the exception.
        /// </summary>
        public string? Reason { get; }
        /// <summary>
        ///     Gets the request object used to send the request.
        /// </summary>
        public IRequest Request { get; }
        /// <summary>
        ///     Gets a collection of json errors describing what went wrong with the request.
        /// </summary>
        public IReadOnlyCollection<MezonJsonError> Errors { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="HttpException" /> class.
        /// </summary>
        /// <param name="httpCode">The HTTP status code returned.</param>
        /// <param name="request">The request that was sent prior to the exception.</param>
        /// <param name="mezonCode">The Mezon status code returned.</param>
        /// <param name="reason">The reason behind the exception.</param>
        public HttpException(HttpStatusCode httpCode, IRequest request, MezonErrorCode mezonCode = MezonErrorCode.GeneralError, string? reason = null, MezonJsonError[]? errors = null)
            : base(CreateMessage(httpCode, (int)mezonCode, reason, errors))
        {
            HttpCode = httpCode;
            Request = request;
            MezonCode = mezonCode;
            Reason = reason;
            Errors = errors?.ToImmutableArray() ?? ImmutableArray<MezonJsonError>.Empty;
        }

        private static string CreateMessage(HttpStatusCode httpCode, int? mezonCode = null, string? reason = null, MezonJsonError[]? errors = null)
        {
            string msg;
            if (mezonCode != null && mezonCode != 0)
            {
                if (reason != null)
                {
                    msg = $"The server responded with error {(int)mezonCode}: {reason}";
                }
                else
                {
                    msg = $"The server responded with error {(int)mezonCode}: {httpCode}";
                }
            }
            else
            {
                if (reason != null)
                {
                    msg = $"The server responded with error {(int)httpCode}: {reason}";
                }
                else
                {
                    msg = $"The server responded with error {(int)httpCode}: {httpCode}";
                }
            }

            if (errors?.Length > 0)
            {
                msg += "\nInner Errors:";
                foreach (var error in errors)
                {
                    if (error.Errors?.Count > 0)
                    {
                        foreach (var innerError in error.Errors)
                        {
                            msg += $"\n{innerError.Code}: {innerError.Message}";
                        }
                    }
                }
            }

            return msg;
        }
    }
}
