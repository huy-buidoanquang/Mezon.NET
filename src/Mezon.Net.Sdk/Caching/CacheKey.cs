using System;

namespace Mezon.Net.Sdk.Caching
{
    /// <summary>
    ///     Redis-safe snapshot key: <c>{env}:{accountId}:{entityType}:{id}</c>.
    /// </summary>
    public readonly struct CacheKey : IEquatable<CacheKey>
    {
        public CacheKey(string environment, long accountId, string entityType, string id)
        {
            if (string.IsNullOrWhiteSpace(environment))
            {
                throw new ArgumentException("Environment is required.", nameof(environment));
            }

            if (string.IsNullOrWhiteSpace(entityType))
            {
                throw new ArgumentException("Entity type is required.", nameof(entityType));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Id is required.", nameof(id));
            }

            ValidateSegment(environment, nameof(environment));
            ValidateSegment(entityType, nameof(entityType));
            ValidateSegment(id, nameof(id));

            Environment = environment;
            AccountId = accountId;
            EntityType = entityType;
            Id = id;
        }

        public string Environment { get; }

        public long AccountId { get; }

        public string EntityType { get; }

        public string Id { get; }

        public string ToRedisKey() => FormattableString.Invariant($"{Environment}:{AccountId}:{EntityType}:{Id}");

        public static CacheKey Parse(string redisKey)
        {
            if (string.IsNullOrWhiteSpace(redisKey))
            {
                throw new ArgumentException("Redis key is required.", nameof(redisKey));
            }

            var firstColon = redisKey.IndexOf(':');
            if (firstColon <= 0)
            {
                throw new FormatException("Cache key must contain at least four colon-separated segments.");
            }

            var secondColon = redisKey.IndexOf(':', firstColon + 1);
            if (secondColon <= firstColon + 1)
            {
                throw new FormatException("Cache key must contain a numeric account id segment.");
            }

            var thirdColon = redisKey.IndexOf(':', secondColon + 1);
            if (thirdColon <= secondColon + 1)
            {
                throw new FormatException("Cache key must contain an entity type segment.");
            }

            if (thirdColon >= redisKey.Length - 1)
            {
                throw new FormatException("Cache key must contain an id segment.");
            }

            var environment = redisKey.Substring(0, firstColon);
            var accountSegment = redisKey.Substring(firstColon + 1, secondColon - firstColon - 1);
            if (!long.TryParse(accountSegment, out var accountId))
            {
                throw new FormatException("Account id segment must be a 64-bit integer.");
            }

            var entityType = redisKey.Substring(secondColon + 1, thirdColon - secondColon - 1);
            var id = redisKey.Substring(thirdColon + 1);
            return new CacheKey(environment, accountId, entityType, id);
        }

        public bool Equals(CacheKey other) =>
            AccountId == other.AccountId
            && string.Equals(Environment, other.Environment, StringComparison.Ordinal)
            && string.Equals(EntityType, other.EntityType, StringComparison.Ordinal)
            && string.Equals(Id, other.Id, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Environment.GetHashCode(StringComparison.Ordinal);
                hash = (hash * 397) ^ AccountId.GetHashCode();
                hash = (hash * 397) ^ EntityType.GetHashCode(StringComparison.Ordinal);
                hash = (hash * 397) ^ Id.GetHashCode(StringComparison.Ordinal);
                return hash;
            }
        }

        public override string ToString() => ToRedisKey();

        public static bool operator ==(CacheKey left, CacheKey right) => left.Equals(right);

        public static bool operator !=(CacheKey left, CacheKey right) => !left.Equals(right);

        private static void ValidateSegment(string value, string paramName)
        {
            if (value.IndexOf(':') >= 0)
            {
                throw new ArgumentException("Segment must not contain ':' characters.", paramName);
            }
        }
    }
}
