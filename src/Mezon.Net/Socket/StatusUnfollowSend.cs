using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a request to stop receiving status updates for a set of users.
    /// </summary>
    public class StatusUnfollowSend
    {
        [JsonPropertyName("status_unfollow")]
        public StatusUnfollowDetails Details { get; set; }
    }

    /// <summary>
    /// Contains the specific details for the status unfollow request.
    /// </summary>
    public class StatusUnfollowDetails
    {
        /// <summary>
        /// The IDs of the users to unfollow.
        /// </summary>
        [JsonPropertyName("user_ids")]
        public List<string> UserIds { get; set; }
    }
}
