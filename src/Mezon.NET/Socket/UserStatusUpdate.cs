using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a request to set the user's own status.
    /// </summary>
    public class UserStatusUpdate
    {
        [JsonPropertyName("status_update")]
        public UserStatusUpdateDetails UserStatusUpdateDetails { get; set; }
    }

    /// <summary>
    /// Contains the specific details for the status update request.
    /// </summary>
    public class UserStatusUpdateDetails
    {
        /// <summary>
        /// The status string to set. If not present, the user will appear offline.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
}
