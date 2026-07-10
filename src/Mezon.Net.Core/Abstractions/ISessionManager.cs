using System;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Core.Abstractions
{
    public interface ISessionManager<TOptions> : IDisposable where TOptions : MezonOptions
    {
        event Func<ISession, Task>? SessionRefreshed;

        Task LoginAsync(long clientId, string clientSecret, bool autoRefreshSession = true);

        Task LoginAsync(ISession session, bool autoRefreshSession = true);

        Task LogoutAsync();

        ISession CurrentSession();

        string GetToken();

        Task<string> GetOrRefreshAsync();
    }
}
