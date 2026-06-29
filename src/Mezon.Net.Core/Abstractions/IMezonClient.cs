using System;
using System.Threading.Tasks;
using Mezon.Net.Core;

namespace Mezon.Net.Abstractions
{
    public interface IMezonClient : IDisposable, IAsyncDisposable
    {
        /// <summary>
        ///     Gets the current state of connection.
        /// </summary>
        ConnectionState ConnectionState { get; }

        /// <summary>
        ///     Gets the token type of the logged-in user.
        /// </summary>
        TokenType TokenType { get; }

        /// <summary>
        ///     Logins to Mezon using the provided session information.
        /// </summary>
        /// <remarks>
        ///     This method will attempt to establish a connection to Mezon using the provided session information. If the login
        ///     is successful, the client will be considered logged in.
        ///     <note type="important">
        ///         This method will return immediately upon being called, to initiate a connection to the Socket, call the <see cref="ConnectAsync"/> method after a successful login.
        ///     </note>
        /// </remarks>
        /// <returns>
        ///     A task that represents the asynchronous login operation. The task result indicates whether the login was successful.
        /// </returns>
        Task<bool> LoginAsync(ISession session);
        Task<bool> LoginAsync();

        /// <summary>
        ///     Logouts from Mezon, closing the connection to the Socket and invalidating the current session. After this method is called, the client will be considered logged out.
        /// </summary>
        /// <returns>
        ///     A task that represents the asynchronous logout operation.
        /// </returns>
        Task LogoutAsync();

        /// <summary>
        ///     Connects to the Mezon Socket. This method should be called after a successful login to establish a connection to the Socket.
        /// </summary>
        /// <returns>
        ///     A task that represents the asynchronous connect operation.
        /// </returns>
        Task ConnectAsync();

        /// <summary>
        ///     Disconnects from the Mezon Socket. This method should be called to close the connection to the Socket.
        /// </summary>
        /// <returns>
        ///     A task that represents the asynchronous disconnect operation.
        /// </returns>
        Task DisconnectAsync();
    }
}
