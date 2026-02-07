using System.Collections.Immutable;
using Mezon.NET.Core;

namespace Mezon.NET.Api
{
    public enum GatewayBucketType
    {
        Unbucketed = 0,
        Identify = 1,
        PresenceUpdate = 2,
    }
    internal struct GatewayBucket
    {
        private static readonly ImmutableDictionary<GatewayBucketType, GatewayBucket> DefsByType;
        private static readonly ImmutableDictionary<BucketId, GatewayBucket> DefsById;

        static GatewayBucket()
        {
            var buckets = new[]
            {
                // Limit is 120/60s, but 3 will be reserved for heartbeats (2 for possible heartbeats in the same timeframe and a possible failure)
                new GatewayBucket(GatewayBucketType.Unbucketed, BucketId.Create(string.Empty, "<gateway-unbucketed>", []), 117, 60),
                new GatewayBucket(GatewayBucketType.Identify, BucketId.Create(string.Empty, "<gateway-identify>", []), 1, 5),
                new GatewayBucket(GatewayBucketType.PresenceUpdate, BucketId.Create(string.Empty, "<gateway-presenceupdate>", []), 5, 60),
            };

            var builder = ImmutableDictionary.CreateBuilder<GatewayBucketType, GatewayBucket>();
            foreach (var bucket in buckets)
            {
                builder.Add(bucket.Type, bucket);
            }

            DefsByType = builder.ToImmutable();

            var builder2 = ImmutableDictionary.CreateBuilder<BucketId, GatewayBucket>();
            foreach (var bucket in buckets)
            {
                builder2.Add(bucket.Id, bucket);
            }

            DefsById = builder2.ToImmutable();
        }

        public static GatewayBucket Get(GatewayBucketType type) => DefsByType[type];
        public static GatewayBucket Get(BucketId id) => DefsById[id];

        public GatewayBucketType Type { get; }
        public BucketId Id { get; }
        public int WindowCount { get; set; }
        public int WindowSeconds { get; set; }

        public GatewayBucket(GatewayBucketType type, BucketId id, int count, int seconds)
        {
            Type = type;
            Id = id;
            WindowCount = count;
            WindowSeconds = seconds;
        }
    }
}
