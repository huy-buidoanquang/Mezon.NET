using System.Threading.Tasks;
using Mezon.Net.Core;

namespace Mezon.Net.Client
{
    public partial class MezonClient
    {
        internal async Task<bool> LoginAsBotInternalAsync(long botId, string token, bool autoRefreshSession)
        {
            await StateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SessionManager.LoginAsync(botId, token, autoRefreshSession).ConfigureAwait(false);
                var session = SessionManager.CurrentSession();
                if (session.IsExpired())
                {
                    return false;
                }

                await LoginInternalAsync(TokenType, session.AuthToken).ConfigureAwait(false);
                return true;
            }
            catch (MezonException)
            {
                throw;
            }
            catch
            {
                return false;
            }
            finally
            {
                StateLock.Release();
            }
        }
    }
}
