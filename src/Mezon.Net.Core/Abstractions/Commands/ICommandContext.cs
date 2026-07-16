namespace Mezon.Net.Abstractions
{
    /// <summary>
    ///     Represents a context of a command. This may include the client, guild, channel, user, and message.
    /// </summary>
    public interface ICommandContext
    {
        /// <summary>
        ///     Gets the <see cref="IMezonClient"/> that the command is executed with.
        /// </summary>
        IMezonClient Client { get; }
    }
}
