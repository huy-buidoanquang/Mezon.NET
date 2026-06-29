using System.Text.Json.Serialization;

namespace Mezon.NET.Api.ApiResponses
{
    /// <summary>
    /// Represents a user authentication session.
    /// </summary>
    public class AuthenticationResponse
    {
        /// <summary>
        /// True if the corresponding account was just created, false otherwise.
        /// </summary>
        [JsonPropertyName("created")]
        public bool Created { get; set; } = false;

        /// <summary>
        /// Refresh token that can be used for session token renewal.
        /// </summary>
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// The authentication token (e.g., JWT).
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier for the user.
        /// </summary>
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Whether to enable "Remember Me" for extended session duration.
        /// </summary>
        [JsonPropertyName("is_remember")]
        public bool IsRemember { get; set; } = false;

        /// <summary>
        /// The unique identifier for the user.
        /// </summary>
        [JsonPropertyName("api_url")]
        public string ApiUrl { get; set; } = string.Empty;
    }
}
