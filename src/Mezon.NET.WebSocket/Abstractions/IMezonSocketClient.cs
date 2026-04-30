using System;
using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Internal.Protos;

namespace Mezon.Net.Abstractions
{
    public interface IMezonSocketClient : IMezonApiClient
    {
        event Func<string, Task> SocketSentMessageEvent;
        Task ConnectAsync();
        Task DisconnectAsync(Exception? ex = null);
        Task Ping(RequestOptions? options = null);
        Task JoinClanChat(long clanId, RequestOptions? options = null);
        Task JoinChannelChat(long clanId, long channelId, int channelType, bool isPublic, RequestOptions? options = null);
    }
}
