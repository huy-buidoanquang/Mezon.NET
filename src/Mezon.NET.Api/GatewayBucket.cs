using Mezon.NET.Core;

namespace Mezon.NET.Api
{
    /// <summary>
    ///     Simple gateway bucket definition without BucketId complexity.
    /// </summary>
    internal struct GatewayBucket
    {
        public static readonly GatewayBucket Unbucketed = new(GatewayBucketType.Unbucketed, 117, 60);
        public static readonly GatewayBucket Identify = new(GatewayBucketType.Identify, 1, 5);
        public static readonly GatewayBucket PresenceUpdate = new(GatewayBucketType.PresenceUpdate, 5, 60);

        public static GatewayBucket Get(GatewayBucketType type) => type switch
        {
            GatewayBucketType.Unbucketed => Unbucketed,
            GatewayBucketType.Identify => Identify,
            GatewayBucketType.PresenceUpdate => PresenceUpdate,
            _ => throw new System.ArgumentOutOfRangeException(nameof(type))
        };

        public GatewayBucketType Type { get; }
        public int WindowCount { get; }
        public int WindowSeconds { get; }

        private GatewayBucket(GatewayBucketType type, int count, int seconds)
        {
            Type = type;
            WindowCount = count;
            WindowSeconds = seconds;
        }
    }
}
