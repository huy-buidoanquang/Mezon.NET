using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Mezon.Net.Core
{
    /// <summary>
    /// Global transport settings applied when creating network transporters.
    /// </summary>
    public static class MezonNetworkSettings
    {
        /// <summary>
        /// Default Mezon socket gateway host (dev).
        /// </summary>
        public const string DefaultSocketHost = "dev-mezon-sock.nccsoft.vn";

        /// <summary>
        /// Default Mezon WebSocket/TCP socket gateway port (dev).
        /// </summary>
        public const int DefaultSocketPort = 4433;

        /// <summary>
        /// Optional SSL certificate validation callback. When null, certificates are accepted (development default).
        /// </summary>
        public static RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; set; }

        internal static bool DefaultValidateServerCertificate(
            object? sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (RemoteCertificateValidationCallback != null)
            {
                return RemoteCertificateValidationCallback(sender!, certificate, chain, sslPolicyErrors);
            }

            return true;
        }
    }
}
