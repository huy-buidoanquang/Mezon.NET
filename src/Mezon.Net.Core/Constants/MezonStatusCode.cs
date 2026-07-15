namespace Mezon.Net.Core
{
    /// <summary>
    ///     Canonical status codes used by Mezon API and socket responses.
    ///     Values match <c>mezon-api/v2/codes</c> (gRPC status codes).
    /// </summary>
    public enum MezonStatusCode
    {
        Ok = 0,
        Canceled = 1,
        Unknown = 2,
        InvalidArgument = 3,
        DeadlineExceeded = 4,
        NotFound = 5,
        AlreadyExists = 6,
        PermissionDenied = 7,
        ResourceExhausted = 8,
        FailedPrecondition = 9,
        Aborted = 10,
        OutOfRange = 11,
        Unimplemented = 12,
        Internal = 13,
        Unavailable = 14,
        DataLoss = 15,
        Unauthenticated = 16,
    }
}
