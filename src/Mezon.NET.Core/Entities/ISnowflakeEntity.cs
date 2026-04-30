using System;

namespace Mezon.Net.Core
{
    /// <summary> Represents a Mezon snowflake entity. </summary>
    public interface ISnowflakeEntity : IEntity<ulong>
    {
        /// <summary>
        ///     Gets when the snowflake was created.
        /// </summary>
        /// <returns>
        ///     A <see cref="DateTimeOffset"/> representing when the entity was first created.
        /// </returns>
        DateTimeOffset CreatedAt { get; }
    }
}
