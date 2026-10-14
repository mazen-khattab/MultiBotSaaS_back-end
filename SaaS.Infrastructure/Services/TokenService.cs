using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Common.Settings;
using SaaS.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SaaS.Infrastructure.Services
{
    /// <summary>
    /// Implementation of JWT token generation using Microsoft's IdentityModel.
    /// Generates cryptographically secure access and refresh tokens with claims.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly IOptionsSnapshot<SecuritySettings> _securitySettings;
        private readonly ILogger<TokenService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHostEnvironment _env;

        public const string AccessTokenCookieName = "X-Access-Token";
        public const string RefreshTokenCookieName = "X-Refresh-Token";

        public TokenService(IOptionsSnapshot<SecuritySettings> securityOptions, ILogger<TokenService> logger, IHttpContextAccessor httpContextAccessor, IHostEnvironment environment)
        {
            _securitySettings = securityOptions;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _env = environment;

            if (string.IsNullOrWhiteSpace(securityOptions.Value.JwtSecret))
                throw new ArgumentException("JwtSecret cannot be empty.", nameof(securityOptions));

            if (string.IsNullOrWhiteSpace(securityOptions.Value.JwtIssuer))
                throw new ArgumentException("JwtIssuer cannot be empty.", nameof(securityOptions));

            if (string.IsNullOrWhiteSpace(securityOptions.Value.JwtAudience))
                throw new ArgumentException("JwtAudience cannot be empty.", nameof(securityOptions));

            // Validate that JwtSecret is at least 256 bits (32 bytes) for HMAC-SHA256
            if (Encoding.UTF8.GetBytes(securityOptions.Value.JwtSecret).Length < 32)
                throw new ArgumentException(
                    "JwtSecret must be at least 256 bits (32 bytes) for HMAC-SHA256 security.",
                    nameof(securityOptions));
        }

        /// <summary>
        /// Generates access and refresh tokens for the specified user.
        /// </summary>
        /// <param name="userId">The identifier of the user for whom the tokens are generated.</param>
        /// <param name="email">The email address used to include user identity information in the access token.</param>
        /// <param name="sessionToken">The current session token used to bind the generated access token to the session.</param>
        /// <returns>An <see cref="AuthenticationResult"/> containing the generated tokens and their expiration times.</returns>
        public string GenerateAccessToken(Guid userId, string email, string sessionToken)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Email, email),
                new(ClaimTypes.Role, "User"),
                new("SessionToken", sessionToken)
            };

            return BuildJwtToken(claims);
        }

        /// <summary>
        /// Generates a JWT access token for the specified system administrator.
        /// </summary>
        /// <param name="admin">The system administrator whose identity and role are included in the token claims.</param>
        /// <returns>The generated JWT access token string.</returns>
        public string GenerateAdminAccessToken(Guid adminId, string email, string role)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, adminId.ToString()),
                new(ClaimTypes.Email, email),
                new(ClaimTypes.Role, role)
            };

            return BuildJwtToken(claims);
        }

        /// <summary>
        /// Creates a new refresh token for either a user or an admin, revoking any previously active tokens for the same owner.
        /// </summary>
        /// <returns>A task that resolves to the generated refresh token string.</returns>
        /// <exception cref="ArgumentException">Thrown when both <paramref name="userId"/> and <paramref name="adminId"/> are null.</exception>
        public string GenerateRefreshToken()
        {
            string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            var utcNow = DateTime.UtcNow;

            var refreshToken = new UserRefreshToken
            {
                Token = token,
                ExpDate = utcNow.AddDays(_securitySettings.Value.RefreshTokenExpirationDays),
                CreatedAt = utcNow,
            };

            return token;
        }

        /// <summary>
        /// Stores the access and refresh tokens in the current response cookies.
        /// </summary>
        /// <param name="accessToken">The access token to write to the cookie store.</param>
        /// <param name="refreshToken">The refresh token to write to the cookie store.</param>
        public void SetAuthCookies(string accessToken, string refreshToken)
        {
            var response = _httpContextAccessor.HttpContext?.Response;
            if (response == null) return;

            var accessTokenOptions = BuildCookieOptions(DateTimeOffset.UtcNow.AddMinutes(15));
            var refreshTokenOptions = BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(7));

            response.Cookies.Append(AccessTokenCookieName, accessToken, accessTokenOptions);
            response.Cookies.Append(RefreshTokenCookieName, refreshToken, refreshTokenOptions);
        }

        /// <summary>
        /// Deletes the authentication cookies from the current response.
        /// </summary>
        public void ClearAuthCookies()
        {
            var response = _httpContextAccessor.HttpContext?.Response;
            if (response == null) return;

            var cookieOptions = BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(-1));

            response.Cookies.Delete(AccessTokenCookieName, cookieOptions);
            response.Cookies.Delete(RefreshTokenCookieName, cookieOptions);
        }

        /// <summary>
        /// Retrieves the access token from the current request cookies.
        /// </summary>
        /// <returns>The access token if present; otherwise, null.</returns>
        public string? GetCookiesAccessToken() => _httpContextAccessor.HttpContext?.Request.Cookies[AccessTokenCookieName];

        /// <summary>
        /// Retrieves the refresh token from the current request cookies.
        /// </summary>
        /// <returns>The refresh token if present; otherwise, null.</returns>
        public string? GetCookiesRefreshToken() => _httpContextAccessor.HttpContext?.Request.Cookies[RefreshTokenCookieName];

        private string BuildJwtToken(IEnumerable<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_securitySettings.Value.JwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_securitySettings.Value.AccessTokenExpirationMinutes),
                Issuer = _securitySettings.Value.JwtIssuer,
                Audience = _securitySettings.Value.JwtAudience,
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private CookieOptions BuildCookieOptions(DateTimeOffset expires) => new()
        {
            HttpOnly = true,
            // SameSite=None requires Secure; use the API's HTTPS origin for cookie requests.
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            Expires = expires
        };
    }
}
