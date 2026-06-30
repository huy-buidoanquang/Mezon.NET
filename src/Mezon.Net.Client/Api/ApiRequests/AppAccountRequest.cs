using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mezon.Net.Api
{
    /// <summary>
    /// Represents an application associated with an API account.
    /// <br/><br/>
    /// You can create your app <see href="https://mezon.ai/developers/applications">here</see> and refer to this documentation: 
    /// <see href="https://mezon.ai/docs/mezon-bot-docs/"/>
    /// </summary>
    public class AppAccountRequest
    {
        /// <summary>
        /// Gets or sets the application ID.
        /// </summary>
        [JsonProperty("appid")]
        public string? AppId { get; set; }

        /// <summary>
        /// Gets or sets the application name.
        /// </summary>
        [JsonProperty("appname")]
        public string? AppName { get; set; }

        /// <summary>
        /// The account token used by apps to access their profile API.
        /// </summary>
        [JsonProperty("token")]
        public string? Token { get; set; }

        /// <summary>
        /// Extra information that will be bundled in the session token.
        /// A dictionary with string keys and string values.
        /// </summary>
        [JsonProperty("vars")]
        public Dictionary<string, string>? Vars { get; set; }
    }
}
