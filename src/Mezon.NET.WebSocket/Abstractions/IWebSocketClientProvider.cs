namespace Mezon.NET.Abstractions
{
    public interface IWebSocketClientProvider
    {
        IWebSocketClient MezonWebSocketClient { get; }
    }
}
