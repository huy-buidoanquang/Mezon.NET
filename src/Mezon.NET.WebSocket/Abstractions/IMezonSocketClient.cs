using System;
using System.Threading.Tasks;
using Mezon.NET.Core;

namespace Mezon.NET.Abstractions
{
    public interface IMezonSocketClient : IMezonApiClient
    {
        Task ConnectAsync();
        Task DisconnectAsync(Exception? ex = null);

        Task JoinClanChat(long clanId, RequestOptions? options = null);
        Task JoinChannelChat(long clanId, long channelId, int channelType, bool isPublic, RequestOptions? options = null);
    }
}
