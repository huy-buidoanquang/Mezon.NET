using Mezon.NET.Core;

namespace Mezon.NET.Api
{
    /// <summary>
    ///     Simple gateway bucket definition without BucketId complexity.
    /// </summary>
    internal struct GatewayBucket
    {
        public static readonly GatewayBucket Unbucketed = new(BucketType.Unbucketed, 117, 60);
        public static readonly GatewayBucket Identify = new(BucketType.Identify, 1, 5);
        public static readonly GatewayBucket PresenceUpdate = new(BucketType.PresenceUpdate, 5, 60);

        public static GatewayBucket Get(BucketType type) => type switch
        {
            BucketType.Unbucketed => Unbucketed,
            BucketType.Identify => Identify,
            BucketType.PresenceUpdate => PresenceUpdate,
            _ => throw new System.ArgumentOutOfRangeException(nameof(type))
        };

        public BucketType Type { get; }
        public int WindowCount { get; }
        public int WindowSeconds { get; }

        private GatewayBucket(BucketType type, int count, int seconds)
        {
            Type = type;
            WindowCount = count;
            WindowSeconds = seconds;
        }
    }
}
