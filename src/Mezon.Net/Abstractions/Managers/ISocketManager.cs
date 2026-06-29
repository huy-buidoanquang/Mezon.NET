using System.Threading;
using System.Threading.Tasks;
using Mezon.NET.Utils;

namespace Mezon.NET.Abstractions
{
    public interface ISocketManager
    {
        bool CreateSocket(WebSocketAdapterEnum webSocketAdapter = WebSocketAdapterEnum.Text);

        Task ConnectSocketAsync(CancellationToken cancellationToken = default);

        Task CloseSocketAsync(bool fireDisconnectEvent, CancellationToken cancellationToken = default);

        Task JoinClanChatAsync(string clanId, CancellationToken cancellationToken = default);
    }
}
