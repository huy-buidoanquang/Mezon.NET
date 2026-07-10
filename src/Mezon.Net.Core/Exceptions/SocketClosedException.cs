using System;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     The exception that is thrown when the WebSocket session is closed by Mezon.
    /// </summary>
    public class SocketClosedException : MezonException
    {
        /// <summary>
        ///     Gets the close code sent by Mezon.
        /// </summary>
        public int CloseCode { get; }
        /// <summary>
        ///     Gets the reason of the interruption.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SocketClosedException" /> using a Mezon close code
        ///     and an optional reason.
        /// </summary>
        public SocketClosedException(int closeCode, string? reason = null)
            : base($"The server sent close {closeCode}{(reason != null ? $": \"{reason}\"" : "")}")
        {
            CloseCode = closeCode;
            Reason = reason ?? string.Empty;
        }
    }
}
