using System.Collections.Generic;
using System.Collections.Immutable;

namespace Mezon.NET.Core
{
    /// <summary>
    ///     Represents a generic parsed json error received from mezon after performing a rest request.
    /// </summary>
    public struct MezonJsonError
    {
        /// <summary>
        ///     Gets the json path of the error.
        /// </summary>
        public string Path { get; }

        /// <summary>
        ///     Gets a collection of errors associated with the specific property at the path.
        /// </summary>
        public IReadOnlyCollection<MezonError> Errors { get; }

        internal MezonJsonError(string path, MezonError[] errors)
        {
            Path = path;
            Errors = errors.ToImmutableArray();
        }
    }

    /// <summary>
    ///     Represents an error with a property.
    /// </summary>
    public struct MezonError
    {
        /// <summary>
        ///     Gets the code of the error.
        /// </summary>
        public string Code { get; }

        /// <summary>
        ///     Gets the message describing what went wrong.
        /// </summary>
        public string Message { get; }

        internal MezonError(string code, string message)
        {
            Code = code;
            Message = message;
        }
    }
}
