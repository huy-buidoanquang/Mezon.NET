using System;
using System.Threading.Tasks;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Core.Abstractions
{
    public interface ISessionManager : IDisposable
    {
        event Func<ISession, Task>? SessionChanged;

        ISession CurrentSession { get; }

        string GetToken();

        Task<string> GetOrRefreshAccessTokenAsync();
    }
}
