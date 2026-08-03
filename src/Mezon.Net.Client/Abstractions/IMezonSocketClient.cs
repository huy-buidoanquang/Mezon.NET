using System;
using System.Threading.Tasks;

namespace Mezon.Net.Abstractions
{
    internal interface IMezonSocketClient : IMezonApiClient
    {
        event Func<string, Task> SocketMessageSent;
        Task ConnectAsync();
        Task DisconnectAsync(Exception? ex = null);
    }
}
