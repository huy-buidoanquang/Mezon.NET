using Mezon.NET.Utils;

namespace Mezon.NET.Abstractions
{
    public interface IWebSocketAdapterFactory
    {
        IWebSocketAdapter Create(WebSocketAdapterEnum adapterType);
    }
}
