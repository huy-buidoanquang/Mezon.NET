using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Mezon.NET.Api.ApiResponses;

namespace Mezon.NET
{
    public class Session
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
        public string Username { get; private set; }
        public string UserId { get; private set; }
        public bool IsRemember { get; private set; }
        public string ApiUrl { get; private set; }

        public Session(AuthenticationResponse authenticationResponse)
        {
            Created = authenticationResponse.Created;
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ApiUrl = authenticationResponse.ApiUrl;
            AuthToken = authenticationResponse.Token;
            RefreshToken = authenticationResponse.RefreshToken;
            InitializeSession(AuthToken, RefreshToken);
        }

        private Session()
        {
            AuthToken = string.Empty;
            RefreshToken = string.Empty;
            UserId = string.Empty;
            Username = string.Empty;
        }

        public void InitializeSession(string authToken, string refreshToken)
        {
            AuthToken = authToken;
            RefreshToken = refreshToken;

            var handler = new JwtSecurityTokenHandler();
            if (!string.IsNullOrEmpty(authToken))
            {
                var decodedAuthToken = handler.ReadJwtToken(authToken);
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

            if (!string.IsNullOrEmpty(refreshToken))
            {
                var decodedRefreshToken = handler.ReadJwtToken(refreshToken);
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

            return DateTimeOffset.FromUnixTimeSeconds(ExpiresAt) < DateTime.UtcNow.AddSeconds(seconds);
        }

        public bool IsRefreshExpiredSoon(int seconds)
        {
            if (RefreshExpiresAt == 0)
            {
                return false;
            }

            return DateTimeOffset.FromUnixTimeSeconds(RefreshExpiresAt) < DateTime.UtcNow.AddSeconds(seconds);
        }

        public bool IsExpired()
        {
            if (ExpiresAt == 0)
            {
                return false;
            }

            return DateTimeOffset.FromUnixTimeSeconds(ExpiresAt) < DateTime.UtcNow;
        }

        public bool IsRefreshExpired()
        {
            if (RefreshExpiresAt == 0)
            {
                return false;
            }

            return DateTimeOffset.FromUnixTimeSeconds(RefreshExpiresAt) < DateTime.UtcNow;
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
