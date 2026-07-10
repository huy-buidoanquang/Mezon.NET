namespace Mezon.Net.Core
{
    /// <summary>
    ///     Well-known error messages returned by mezon-sock.
    ///     Match against <see cref="MezonApiException.Detail"/> when handling domain-specific failures.
    /// </summary>
    public static class MezonApiErrors
    {
        public const string ClanNotFound = "clan not found";
        public const string ChannelNotFound = "channel not found";
        public const string CategoryNotFound = "category not found";
        public const string UserNotFound = "user not found";
        public const string InviteNotFound = "invite not found";
        public const string InviteExpired = "invite link has expired";

        public const string PermissionDenied = "permission denied";
        public const string ClanPermissionDenied = "clan permission denied";
        public const string ChannelPermissionDenied = "channel permission denied";
        public const string UserPermissionDenied = "user permission denied";
        public const string CategoryPermissionDenied = "category permission denied";
        public const string RolePermissionDenied = "role permission denied";

        public const string Unauthorized = "authentication required";
        public const string InvalidToken = "invalid authentication token";
        public const string TokenExpired = "authentication token has expired";

        public const string InternalServer = "internal server error occurred";
        public const string ServiceUnavailable = "service temporarily unavailable";
        public const string ResourceNotFound = "requested resource not found";
    }
}
