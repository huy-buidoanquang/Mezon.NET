using System;

namespace Mezon.Net.Sdk.Commands
{
    public interface ICommandCooldownStore
    {
        bool TryBegin(string commandKey, long userId, TimeSpan cooldown, out TimeSpan remaining);
    }

    public sealed class InMemoryCommandCooldownStore : ICommandCooldownStore
    {
        private readonly object _gate = new object();
        private readonly System.Collections.Generic.Dictionary<(string CommandKey, long UserId), DateTimeOffset> _expiresAt =
            new System.Collections.Generic.Dictionary<(string, long), DateTimeOffset>();

        public bool TryBegin(string commandKey, long userId, TimeSpan cooldown, out TimeSpan remaining)
        {
            if (cooldown <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                return true;
            }

            var now = DateTimeOffset.UtcNow;
            var key = (commandKey, userId);
            lock (_gate)
            {
                if (_expiresAt.TryGetValue(key, out var expiresAt))
                {
                    if (expiresAt > now)
                    {
                        remaining = expiresAt - now;
                        return false;
                    }
                }

                _expiresAt[key] = now.Add(cooldown);
                remaining = TimeSpan.Zero;
                return true;
            }
        }
    }
}
