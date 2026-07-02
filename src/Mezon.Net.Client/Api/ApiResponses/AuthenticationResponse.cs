using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    /// <summary>
    /// Represents a user authentication session.
    /// </summary>
    public class AuthenticationResponse
    {
        /// <summary>
        /// True if the corresponding account was just created, false otherwise.
        /// </summary>
        [JsonProperty("created")]
        public bool Created { get; set; } = false;

        /// <summary>
        /// Refresh token that can be used for session token renewal.
        /// </summary>
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// The authentication token (e.g., JWT).
        /// </summary>
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// The session id for the user.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier for the user.
        /// </summary>
        [JsonProperty("user_id")]
        public long UserId { get; set; }

        /// <summary>
        /// Whether to enable "Remember Me" for extended session duration.
        /// </summary>
        [JsonProperty("is_remember")]
        public bool IsRemember { get; set; } = false;

        /// <summary>
        /// The unique identifier for the user.
        /// </summary>
        [JsonProperty("api_url")]
        public string ApiUrl { get; set; } = string.Empty;


        /// <summary>
        /// The unique identifier for the user.
        /// </summary>
        [JsonProperty("ws_url")]
        public string? WsUrl { get; set; } = string.Empty;

        [JsonProperty("tcp_url")]
        public string? TcpUrl { get; set; } = string.Empty;
    }
}
