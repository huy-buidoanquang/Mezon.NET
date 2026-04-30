using System.Collections.Immutable;
using Mezon.Net.Core;

namespace Mezon.Net.Queue
{
    public enum ApiBucketType
    {
        Unbucketed = 0,
        SendEdit = 1
    }
    internal struct ApiBucket
    {
        private static readonly ImmutableDictionary<ApiBucketType, ApiBucket> DefsByType;
        private static readonly ImmutableDictionary<BucketId, ApiBucket> DefsById;

        static ApiBucket()
        {
            var buckets = new[]
            {
                new ApiBucket(ApiBucketType.Unbucketed, BucketId.Create(string.Empty, "<unbucketed>", []), 10, 10),
                new ApiBucket(ApiBucketType.SendEdit, BucketId.Create(string.Empty, "<send_edit>", []), 10, 10)
            };

            var builder = ImmutableDictionary.CreateBuilder<ApiBucketType, ApiBucket>();
            foreach (var bucket in buckets)
            {
                builder.Add(bucket.Type, bucket);
            }

            DefsByType = builder.ToImmutable();

            var builder2 = ImmutableDictionary.CreateBuilder<BucketId, ApiBucket>();
            foreach (var bucket in buckets)
            {
                builder2.Add(bucket.Id, bucket);
            }

            DefsById = builder2.ToImmutable();
        }

        public static ApiBucket Get(ApiBucketType type) => DefsByType[type];
        public static ApiBucket Get(BucketId id) => DefsById[id];

        public ApiBucketType Type { get; }
        public BucketId Id { get; }
        public int WindowCount { get; }
        public int WindowSeconds { get; }

        public ApiBucket(ApiBucketType type, BucketId id, int count, int seconds)
        {
            Type = type;
            Id = id;
            WindowCount = count;
            WindowSeconds = seconds;
        }
    }
}
