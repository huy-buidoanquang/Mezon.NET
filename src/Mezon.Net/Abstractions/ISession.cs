using System;

namespace Mezon.NET.Abstractions
{
    public interface ISession
    {
        string AuthToken { get; }
        string RefreshToken { get; }
        bool Created { get; }
        long CreatedAt { get; }
        long ExpiresAt { get; }
        long RefreshExpiresAt { get; }
        string Username { get; }
        string UserId { get; }
        bool IsExpired(DateTime now);
        bool IsRefreshExpired(DateTime now);
    }
}
