namespace Mezon.Net.Abstractions
{
    public interface ISession
    {
        public string AuthToken { get; }
        public string RefreshToken { get; }
        public bool Created { get; }
        public long CreatedAt { get; }
        public long ExpiresAt { get; }
        public long RefreshExpiresAt { get; }
        public string? Username { get; }
        public string? UserId { get; }
        public bool IsRemember { get; }
        public string? ApiUrl { get; }
        public string? WsUrl { get; }

        bool IsExpiredSoon(int seconds);

        bool IsRefreshExpiredSoon(int seconds);

        bool IsExpired();

        bool IsRefreshExpired();
    }
}
