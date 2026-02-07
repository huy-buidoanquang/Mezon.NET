using System.Threading.Tasks;

namespace Mezon.NET.Abstractions
{
    public interface ISessionManager
    {
        Task<bool> AuthenticateAsync(string token, bool autoRefreshSession = true);

        Task<bool> LogoutAsync();

        Session CurrentSession();
    }
}
