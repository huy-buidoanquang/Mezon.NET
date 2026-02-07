using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mezon.NET.Socket
{
    /// <summary>
    /// Represents a request to start receiving status updates for a set of users.
    /// </summary>
    public class StatusFollowSend
    {
        [JsonPropertyName("status_follow")]
        public StatusFollowDetails StatusFollowDetails { get; set; }
    }

    /// <summary>
    /// Contains the specific details for the status follow request.
    /// </summary>
    public class StatusFollowDetails
    {
        /// <summary>
        /// The IDs of the users to follow.
        /// </summary>
        [JsonPropertyName("user_ids")]
        public List<string> UserIds { get; set; }
    }
}
