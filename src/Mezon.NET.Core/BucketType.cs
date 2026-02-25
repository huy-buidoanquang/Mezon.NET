namespace Mezon.NET.Core
{
    /// <summary>
    ///     Bucket types for WebSocket rate limiting.
    /// </summary>
    public enum BucketType
    {
        Unbucketed = 0,
        Identify = 1,
        PresenceUpdate = 2,
    }
}
