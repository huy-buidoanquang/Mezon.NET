using System;
using System.Threading.Tasks;

namespace Mezon.NET.Core.Abstractions
{
    public interface IMezonClient : IDisposable, IAsyncDisposable
    {
        MezonConfiguration ClientConfiguration { get; }
        /// <summary>
        ///     Gets the current state of connection.
        /// </summary>
        ConnectionState ConnectionState { get; }

        /// <summary>
        ///     Gets the token type of the logged-in user.
        /// </summary>
        TokenType TokenType { get; }

        /// <summary>
        ///     Starts the connection between Mezon and the client..
        /// </summary>
        /// <remarks>
        ///     This method will initialize the connection between the client and Mezon.
        ///     <note type="important">
        ///         This method will immediately return after it is called, as it will initialize the connection on
        ///         another thread.
        ///     </note>
        /// </remarks>
        /// <returns>
        ///     A task that represents the asynchronous start operation.
        /// </returns>
        Task<bool> LoginAsync();

        Task<bool> LoginAsync(ISession session);
        /// <summary>
        ///     Stops the connection between Mezon and the client.
        /// </summary>
        /// <returns>
        ///     A task that represents the asynchronous stop operation.
        /// </returns>
        Task LogoutAsync();

        /// <summary>
        /// Close the socket connection
        /// </summary>
        void CloseSocket();
    }
}
