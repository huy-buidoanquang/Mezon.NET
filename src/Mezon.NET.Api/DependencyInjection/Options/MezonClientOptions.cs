using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mezon.NET.DependencyInjection
{
    public class MezonClientOptions : IValidatableObject
    {
        private const string DefaultToken = "MEZON_APPICAION_TOKEN";
        private const string DefaultHost = "gw.mezon.ai";
        private const int DefaultPort = 443;
        private const bool DefaultSSL = true;
        private const bool DefaultAutoRefreshSession = true;

        public string AppToken { get; set; } = DefaultToken;
        public string Host { get; set; } = DefaultHost;
        public int Port { get; set; } = DefaultPort;
        public bool UseSSL { get; set; } = DefaultSSL;
        public bool AutoRefreshSession { get; set; } = DefaultAutoRefreshSession;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(AppToken) || AppToken.Equals(DefaultToken))
            {
                yield return new ValidationResult("No AppToken defined in MezonClientOptions config", new[] { nameof(AppToken) });
            }
        }
    }
}
