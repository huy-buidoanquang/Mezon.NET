namespace Mezon.Net.Abstractions
{
    public interface IWebSocketClientProvider
    {
        IWebSocketClient MezonWebSocketClient { get; }
    }
}
