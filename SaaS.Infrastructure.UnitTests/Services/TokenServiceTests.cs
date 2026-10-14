using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SaaS.Application.Common.Settings;
using SaaS.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SaaS.Infrastructure.UnitTests.Services
{
    public class TokenServiceTests
    {
        private readonly Mock<IOptionsSnapshot<SecuritySettings>> _mockOptions;
        private readonly Mock<ILogger<TokenService>> _mockLogger;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly Mock<IHostEnvironment> _mockEnvironment;
        private readonly SecuritySettings _securitySettings;

        public TokenServiceTests()
        {
            _mockOptions = new Mock<IOptionsSnapshot<SecuritySettings>>();
            _mockLogger = new Mock<ILogger<TokenService>>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockEnvironment = new Mock<IHostEnvironment>();

            _securitySettings = new SecuritySettings
            {
                JwtSecret = "a-very-long-secret-key-that-is-at-least-32-bytes-long!",
                JwtIssuer = "https://issuer.multibotsaas.com",
                JwtAudience = "https://audience.multibotsaas.com",
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 7
            };

            _mockOptions.Setup(x => x.Value).Returns(_securitySettings);
            _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");
        }

        private TokenService CreateService()
        {
            return new TokenService(
                _mockOptions.Object,
                _mockLogger.Object,
                _mockHttpContextAccessor.Object,
                _mockEnvironment.Object);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_ShouldThrowArgumentException_WhenJwtSecretIsNullOrWhiteSpace(string? secret)
        {
            // Arrange
            _securitySettings.JwtSecret = secret!;

            // Act
            Action act = () => CreateService();

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*JwtSecret cannot be empty*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_ShouldThrowArgumentException_WhenJwtIssuerIsNullOrWhiteSpace(string? issuer)
        {
            // Arrange
            _securitySettings.JwtIssuer = issuer!;

            // Act
            Action act = () => CreateService();

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*JwtIssuer cannot be empty*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_ShouldThrowArgumentException_WhenJwtAudienceIsNullOrWhiteSpace(string? audience)
        {
            // Arrange
            _securitySettings.JwtAudience = audience!;

            // Act
            Action act = () => CreateService();

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*JwtAudience cannot be empty*");
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenJwtSecretIsShorterThan32Bytes()
        {
            // Arrange (20 characters = 20 bytes < 32 bytes)
            _securitySettings.JwtSecret = "short-secret-key-123";

            // Act
            Action act = () => CreateService();

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*JwtSecret must be at least 256 bits*");
        }

        [Fact]
        public void GenerateAccessToken_ShouldReturnValidJwt_WithUserClaims()
        {
            // Arrange
            var service = CreateService();
            var userId = Guid.NewGuid();
            var email = "user@example.com";
            var sessionToken = "session-guid-12345";

            // Act
            var tokenString = service.GenerateAccessToken(userId, email, sessionToken);

            // Assert
            tokenString.Should().NotBeNullOrWhiteSpace();

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(tokenString);

            jwtToken.Issuer.Should().Be(_securitySettings.JwtIssuer);
            jwtToken.Audiences.Should().Contain(_securitySettings.JwtAudience);

            var claims = jwtToken.Claims.ToList();
            claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "nameid")
                .Which.Value.Should().Be(userId.ToString());
            claims.Should().Contain(c => c.Type == ClaimTypes.Email || c.Type == "email")
                .Which.Value.Should().Be(email);
            claims.Should().Contain(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Which.Value.Should().Be("User");
            claims.Should().Contain(c => c.Type == "SessionToken" && c.Value == sessionToken);

            jwtToken.ValidTo.Should().BeAfter(DateTime.UtcNow.AddMinutes(10));
        }

        [Fact]
        public void GenerateAdminAccessToken_ShouldReturnValidJwt_WithAdminClaims()
        {
            // Arrange
            var service = CreateService();
            var adminId = Guid.NewGuid();
            var email = "admin@example.com";
            var role = "SuperAdmin";

            // Act
            var tokenString = service.GenerateAdminAccessToken(adminId, email, role);

            // Assert
            tokenString.Should().NotBeNullOrWhiteSpace();

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(tokenString);

            jwtToken.Issuer.Should().Be(_securitySettings.JwtIssuer);
            jwtToken.Audiences.Should().Contain(_securitySettings.JwtAudience);

            var claims = jwtToken.Claims.ToList();
            claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "nameid")
                .Which.Value.Should().Be(adminId.ToString());
            claims.Should().Contain(c => c.Type == ClaimTypes.Email || c.Type == "email")
                .Which.Value.Should().Be(email);
            claims.Should().Contain(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Which.Value.Should().Be(role);
        }

        [Fact]
        public void GenerateRefreshToken_ShouldReturnCryptographicallySecureBase64UrlString()
        {
            // Arrange
            var service = CreateService();

            // Act
            var token1 = service.GenerateRefreshToken();
            var token2 = service.GenerateRefreshToken();

            // Assert
            token1.Should().NotBeNullOrWhiteSpace();
            token2.Should().NotBeNullOrWhiteSpace();
            token1.Should().NotBe(token2);

            // Must be base64url safe (no +, /, or =)
            token1.Should().NotContain("+");
            token1.Should().NotContain("/");
            token1.Should().NotContain("=");
        }

        [Fact]
        public void SetAuthCookies_ShouldDoNothing_WhenHttpContextIsNull()
        {
            // Arrange
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var service = CreateService();

            // Act
            Action act = () => service.SetAuthCookies("accessToken", "refreshToken");

            // Assert
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        public void SetAuthCookies_ShouldAppendCookiesWithCorrectOptions(string environment)
        {
            // Arrange
            _mockEnvironment.Setup(x => x.EnvironmentName).Returns(environment);
            var httpContext = new DefaultHttpContext();
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var service = CreateService();

            // Act
            service.SetAuthCookies("test-access-token", "test-refresh-token");

            // Assert
            var setCookieHeaders = httpContext.Response.Headers.SetCookie.ToList();
            setCookieHeaders.Should().HaveCount(2);

            var accessCookie = setCookieHeaders.FirstOrDefault(h => h!.Contains(TokenService.AccessTokenCookieName));
            accessCookie.Should().NotBeNull();
            accessCookie.Should().Contain("httponly");
            accessCookie.Should().Contain("secure");
            accessCookie.Should().Contain("samesite=none");
            accessCookie.Should().Contain("path=/");
            accessCookie.Should().NotContain("domain=");

            var refreshCookie = setCookieHeaders.FirstOrDefault(h => h!.Contains(TokenService.RefreshTokenCookieName));
            refreshCookie.Should().NotBeNull();
            refreshCookie.Should().Contain("httponly");
            refreshCookie.Should().Contain("secure");
            refreshCookie.Should().Contain("samesite=none");
            refreshCookie.Should().Contain("path=/");
            refreshCookie.Should().NotContain("domain=");
        }

        [Fact]
        public void SetAuthCookies_ShouldSetSecureFlag_InDevelopment()
        {
            // Arrange
            _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Development");
            var httpContext = new DefaultHttpContext();
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var service = CreateService();

            // Act
            service.SetAuthCookies("test-access-token", "test-refresh-token");

            // Assert
            var setCookieHeaders = httpContext.Response.Headers.SetCookie.ToList();
            setCookieHeaders.Should().HaveCount(2);

            var accessCookie = setCookieHeaders.FirstOrDefault(h => h!.Contains(TokenService.AccessTokenCookieName));
            accessCookie.Should().NotBeNull();
            accessCookie.Should().Contain("secure");
            accessCookie.Should().Contain("samesite=none");
            accessCookie.Should().Contain("path=/");
        }

        [Fact]
        public void ClearAuthCookies_ShouldDoNothing_WhenHttpContextIsNull()
        {
            // Arrange
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var service = CreateService();

            // Act
            Action act = () => service.ClearAuthCookies();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void ClearAuthCookies_ShouldDeleteCookiesFromResponse()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var service = CreateService();

            // Act
            service.ClearAuthCookies();

            // Assert
            var setCookieHeaders = httpContext.Response.Headers.SetCookie.ToList();
            setCookieHeaders.Should().HaveCount(2);

            // Deletion in ASP.NET Core sets expires to epoch in the past and value empty
            var accessCookie = setCookieHeaders.FirstOrDefault(h => h!.Contains(TokenService.AccessTokenCookieName));
            accessCookie.Should().NotBeNull();
            accessCookie.Should().Contain("expires=");
            accessCookie.Should().Contain("path=/");

            var refreshCookie = setCookieHeaders.FirstOrDefault(h => h!.Contains(TokenService.RefreshTokenCookieName));
            refreshCookie.Should().NotBeNull();
            refreshCookie.Should().Contain("expires=");
            refreshCookie.Should().Contain("path=/");
        }

        [Fact]
        public void GetCookiesAccessToken_ShouldReturnToken_WhenPresentInRequestCookies()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Cookie = $"{TokenService.AccessTokenCookieName}=my-access-jwt; {TokenService.RefreshTokenCookieName}=my-refresh-jwt";
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var service = CreateService();

            // Act
            var token = service.GetCookiesAccessToken();

            // Assert
            token.Should().Be("my-access-jwt");
        }

        [Fact]
        public void GetCookiesAccessToken_ShouldReturnNull_WhenHttpContextIsNull()
        {
            // Arrange
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var service = CreateService();

            // Act
            var token = service.GetCookiesAccessToken();

            // Assert
            token.Should().BeNull();
        }

        [Fact]
        public void GetCookiesRefreshToken_ShouldReturnToken_WhenPresentInRequestCookies()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Cookie = $"{TokenService.AccessTokenCookieName}=my-access-jwt; {TokenService.RefreshTokenCookieName}=my-refresh-jwt";
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var service = CreateService();

            // Act
            var token = service.GetCookiesRefreshToken();

            // Assert
            token.Should().Be("my-refresh-jwt");
        }

        [Fact]
        public void GetCookiesRefreshToken_ShouldReturnNull_WhenCookieNotPresent()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var service = CreateService();

            // Act
            var token = service.GetCookiesRefreshToken();

            // Assert
            token.Should().BeNull();
        }
    }
}
