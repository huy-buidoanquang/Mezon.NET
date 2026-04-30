using System;
using System.Collections.Generic;
using System.Text;

namespace Mezon.Net.Abstractions
{
    /// <summary>
    ///     Represents a context of a command. This may include the client, guild, channel, user, and message.
    /// </summary>
    public interface ICommandContext
    {
        /// <summary>
        ///     Gets the <see cref="IMezonClient" /> that the command is executed with.
        /// </summary>
        IMezonClient Client { get; }
        /// <summary>
        ///     Gets the <see cref="IGuild" /> that the command is executed in.
        /// </summary>
        //IMezon Guild { get; }
        ///// <summary>
        /////     Gets the <see cref="IMessageChannel" /> that the command is executed in.
        ///// </summary>
        //IMezonChannel Channel { get; }
        ///// <summary>
        /////     Gets the <see cref="IUser" /> who executed the command.
        ///// </summary>
        //IUser User { get; }
        ///// <summary>
        /////     Gets the <see cref="IUserMessage" /> that the command is interpreted from.
        ///// </summary>
        //IUserMessage Message { get; }
    }
}
