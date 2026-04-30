using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Mezon.Net.Core;
using Mezon.Net.Abstractions;

namespace Mezon.Net.Api
{
    public class Session : ISession
    {
        private const string ExpirationTimeClaim = "exp";
        private const string UserIDClaim = "uid";
        private const string UserNameClaim = "usn";

        public string AuthToken { get; private set; }
        public string RefreshToken { get; private set; }
        public bool Created { get; }
        public long CreatedAt { get; }
        public long ExpiresAt { get; private set; }
        public long RefreshExpiresAt { get; private set; }
        public string? Username { get; private set; }
        public string? UserId { get; private set; }
        public bool IsRemember { get; private set; }
        public string? ApiUrl { get; private set; }
        public string? WsUrl { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public Session(AuthenticationResponse authenticationResponse)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            Created = authenticationResponse.Created;
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ApiUrl = authenticationResponse.ApiUrl;
            WsUrl = authenticationResponse.WsUrl;
            AuthToken = authenticationResponse.Token ?? string.Empty;
            RefreshToken = authenticationResponse.RefreshToken ?? string.Empty;
            InitializeSession();
        }

        private Session()
        {
            AuthToken = string.Empty;
            RefreshToken = string.Empty;
            UserId = string.Empty;
            Username = string.Empty;
        }

        public void InitializeSession()
        {
            Check.NotNullOrEmpty(AuthToken, nameof(AuthToken));
            Check.NotNullOrEmpty(RefreshToken, nameof(RefreshToken));

            var handler = new JwtSecurityTokenHandler();
            if (!string.IsNullOrEmpty(AuthToken))
            {
                var decodedAuthToken = handler.ReadJwtToken(AuthToken);
                ExpiresAt = long.Parse(decodedAuthToken.Claims.First(c => c.Type == ExpirationTimeClaim).Value);
                UserId = decodedAuthToken.Claims.First(c => c.Type == UserIDClaim).Value;
                Username = decodedAuthToken.Claims.First(c => c.Type == UserNameClaim).Value;
            }
            else
            {
                ExpiresAt = 0;
                UserId = string.Empty;
                Username = string.Empty;
            }

            if (!string.IsNullOrEmpty(RefreshToken))
            {
                var decodedRefreshToken = handler.ReadJwtToken(RefreshToken);
                RefreshExpiresAt = long.Parse(decodedRefreshToken.Claims.First(c => c.Type == ExpirationTimeClaim).Value);
            }
            else
            {
                RefreshExpiresAt = 0;
            }
        }

        public bool IsExpiredSoon(int seconds)
        {
            if (ExpiresAt == 0)
            {
                return false;
            }

            return DateTimeOffset.FromUnixTimeSeconds(ExpiresAt) < DateTimeOffset.UtcNow.AddSeconds(seconds);
        }

        public bool IsRefreshExpiredSoon(int seconds)
        {
            if (RefreshExpiresAt == 0)
            {
                return false;
            }

            return DateTimeOffset.FromUnixTimeSeconds(RefreshExpiresAt) < DateTimeOffset.UtcNow.AddSeconds(seconds);
        }

        public bool IsExpired()
        {
            if (ExpiresAt == 0)
            {
                return false;
            }

            return DateTimeOffset.FromUnixTimeSeconds(ExpiresAt) < DateTimeOffset.UtcNow;
        }

        public bool IsRefreshExpired()
        {
            if (RefreshExpiresAt == 0)
            {
                return false;
            }

            return DateTimeOffset.FromUnixTimeSeconds(RefreshExpiresAt) < DateTimeOffset.UtcNow;
        }

        public static Session Restore(AuthenticationResponse authenticationResponse)
        {
            return new Session(authenticationResponse);
        }

        public static Session NullSession()
        {
            return new Session();
        }
    }
}
