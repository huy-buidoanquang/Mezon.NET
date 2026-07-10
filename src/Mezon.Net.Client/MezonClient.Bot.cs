using System.Threading.Tasks;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Client
{
    public static class MezonClientBotExtensions
    {
        /// <summary>
        /// Authenticates a bot application using <c>botId</c> and API token (parity mezon-sdk <c>login()</c> auth step).
        /// Call <see cref="IMezonClient.ConnectAsync"/> after a successful login.
        /// </summary>
        public static Task<bool> LoginAsBotAsync(this MezonClient client, long botId, string token, bool autoRefreshSession = true)
            => client.LoginAsBotInternalAsync(botId, token, autoRefreshSession);
    }
}
