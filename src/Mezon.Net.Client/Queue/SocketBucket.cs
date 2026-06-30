using System.Collections.Immutable;
using Mezon.Net.Core;

namespace Mezon.Net.Queue
{
    public enum SocketBucketType
    {
        Unbucketed = 0,
        Identify = 1,
        PresenceUpdate = 2,
    }

    internal struct SocketBucket
    {
        private static readonly ImmutableDictionary<SocketBucketType, SocketBucket> DefsByType;
        private static readonly ImmutableDictionary<BucketId, SocketBucket> DefsById;

        static SocketBucket()
        {
            var buckets = new[]
            {
                // Limit is 120/60s, but 3 will be reserved for heartbeats (2 for possible heartbeats in the same timeframe and a possible failure)
                new SocketBucket(SocketBucketType.Unbucketed, BucketId.Create(string.Empty, "<socket-unbucketed>", []), 117, 60),
                new SocketBucket(SocketBucketType.Identify, BucketId.Create(string.Empty, "<socket-identify>", []), 1, 5),
                new SocketBucket(SocketBucketType.PresenceUpdate, BucketId.Create(string.Empty, "<socket-presenceupdate>", []), 5, 60),
            };

            var builder = ImmutableDictionary.CreateBuilder<SocketBucketType, SocketBucket>();
            foreach (var bucket in buckets)
            {
                builder.Add(bucket.Type, bucket);
            }
            DefsByType = builder.ToImmutable();

            var builder2 = ImmutableDictionary.CreateBuilder<BucketId, SocketBucket>();
            foreach (var bucket in buckets)
            {
                builder2.Add(bucket.Id, bucket);
            }
            DefsById = builder2.ToImmutable();
        }

        public static SocketBucket Get(SocketBucketType type) => DefsByType[type];
        public static SocketBucket Get(BucketId id) => DefsById[id];

        public SocketBucketType Type { get; }
        public BucketId Id { get; }
        public int WindowCount { get; set; }
        public int WindowSeconds { get; set; }

        public SocketBucket(SocketBucketType type, BucketId id, int count, int seconds)
        {
            Type = type;
            Id = id;
            WindowCount = count;
            WindowSeconds = seconds;
        }
    }
}
