using System;
using System.Threading.Tasks;
using Mezon.Net.Core;

namespace Mezon.Net.Abstractions
{
    public interface IMezonSocketClient : IMezonApiClient
    {
        event Func<string, Task> SocketMessageSent;
        Task ConnectAsync();
        Task DisconnectAsync(Exception? ex = null);
        Task JoinClanChat(long clanId, RequestOptions? options = null);
        Task JoinChannelChat(long clanId, long channelId, int channelType, bool isPublic, RequestOptions? options = null);
    }
}
