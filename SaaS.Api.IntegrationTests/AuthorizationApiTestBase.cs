using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using API.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SaaS.Api.Controllers.v1;
using SaaS.Api.Middlewares;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Common.Services;
using SaaS.Application.Common.Settings;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Infrastructure.Extensions;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Services;

namespace SaaS.Api.IntegrationTests;

// Each test gets an isolated relational database and the production HTTP/auth/CQRS pipeline.
// Only outbound integrations are mocked; no requests reach a worker or external service.
public abstract class AuthorizationApiTestBase : IAsyncLifetime
{
    protected static readonly Guid UserA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid UserB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid Admin = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid SuperAdmin = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected const string SessionA = "authorization-session-a";
    protected const string SessionB = "authorization-session-b";
    protected const string Password = "authorization-test-password!";
    protected const string CookiesPayload = "{\"session\":\"connected-account-credential-secret\"}";
    private static readonly Lazy<string> PasswordHash = new(() => BCrypt.Net.BCrypt.EnhancedHashPassword(Password, 11));
    protected readonly Mock<INetworkClient> Network = new();
    protected readonly ConcurrentQueue<string> Logs = new();
    private readonly SecuritySettings _security = new()
    {
        JwtSecret = "authorization-pass-signing-key-at-least-32-bytes", JwtIssuer = "authorization-tests",
        JwtAudience = "authorization-tests", PasswordWorkFactor = 11, EncryptionKey = "authorization-test-encryption-key"
    };
    private IHost _host = null!;
    private SqliteConnection _connection = null!;
    protected HttpClient Client = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        Network.Setup(value => value.PostJsonAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<ExternalSystem>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(NetworkResult.Ok(200));
        var resolver = new Mock<IN8nWebhookResolver>();
        resolver.Setup(value => value.GetWebhookUrl(It.IsAny<string>())).Returns("https://worker.example.test/webhook");
        _host = await new HostBuilder().ConfigureLogging(logging =>
                logging.ClearProviders().SetMinimumLevel(LogLevel.Information).AddProvider(new CaptureLoggerProvider(Logs)))
            .ConfigureWebHost(web => web.UseTestServer().UseEnvironment("Production")
                .ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
                    services.AddJwtAuthentication(_security);
                    services.AddAuthorization();
                    services.AddHttpContextAccessor();
                    services.AddMediatRWithValidation();
                    services.AddScoped<AppDbContext>(_ => new SqliteAppDbContext(
                        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options));
                    services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
                    services.Configure<SecuritySettings>(options =>
                    {
                        options.JwtSecret = _security.JwtSecret; options.JwtIssuer = _security.JwtIssuer;
                        options.JwtAudience = _security.JwtAudience; options.PasswordWorkFactor = 11;
                        options.EncryptionKey = _security.EncryptionKey;
                    });
                    services.Configure<GeneralSettings>(_ => { });
                    services.Configure<WorkerOptions>(options => options.WorkerSecret = "authorization-worker-key");
                    services.Configure<N8nOptions>(options => options.N8nSecret = "authorization-webhook-key");
                    services.AddScoped<IPasswordHasherService, PasswordHasherService>();
                    services.AddScoped<ITokenService, TokenService>();
                    services.AddScoped<IAuthSessionService, AuthSessionService>();
                    services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
                    services.AddScoped<IUserBotService, UserBotService>();
                    services.AddSingleton<IEncryptionService, EncryptionService>();
                    services.AddScoped<IAdminUserPersistenceService, AdminUserPersistenceService>();
                    services.AddScoped<IAdminBotPersistenceService, AdminBotPersistenceService>();
                    services.AddScoped<IAdminSubscriptionPersistenceService, AdminSubscriptionPersistenceService>();
                    services.AddSingleton(Network.Object);
                    services.AddSingleton(resolver.Object);
                    services.AddSingleton(Mock.Of<IAppNotificationService>());
                })
                .Configure(app =>
                {
                    app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
                    app.UseMiddleware<ExceptionMiddleware>();
                    app.UseMiddleware<SingleActiveSessionMiddleware>();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                })).StartAsync();
        Client = _host.GetTestClient();
        Client.BaseAddress = new Uri("https://api.example.test");
        await WithDb(async db =>
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(new User { Id = UserA, Email = "a@example.test", FullName = "User A", PasswordHash = PasswordHash.Value, CurrentSessionToken = SessionA },
                new User { Id = UserB, Email = "b@example.test", FullName = "User B", PasswordHash = PasswordHash.Value, CurrentSessionToken = SessionB });
            db.SystmeAdmins.AddRange(new SystemAdmin { Id = Admin, Email = "admin@example.test", FullName = "Admin", Role = "Admin", PasswordHash = PasswordHash.Value },
                new SystemAdmin { Id = SuperAdmin, Email = "super@example.test", FullName = "Super", Role = "SuperAdmin", PasswordHash = PasswordHash.Value });
            db.Bots.AddRange(new Bot { Id = 1, Name = "Shared", UiModuleCode = "SHARED" },
                new Bot { Id = 2, Name = "Other", UiModuleCode = "OTHER" },
                new Bot { Id = 3, Name = "Second owned", UiModuleCode = "SECOND-OWNED" });
            db.UserBots.AddRange(new UserBot { UserId = UserA, BotId = 1 }, new UserBot { UserId = UserA, BotId = 3 },
                new UserBot { UserId = UserB, BotId = 1 }, new UserBot { UserId = UserB, BotId = 2 });
            var encrypted = _host.Services.GetRequiredService<IEncryptionService>().Encrypt(CookiesPayload);
            db.ConnectedAccounts.AddRange(Account(11, UserA, 1, encrypted), Account(12, UserB, 1, encrypted),
                Account(13, UserA, 3, encrypted), Account(14, UserA, 2, encrypted));
            db.TargetGroups.AddRange(new TargetGroup { Id = 21, UserId = UserA, BotId = 1, GroupName = "Own group" },
                new TargetGroup { Id = 22, UserId = UserB, BotId = 1, GroupName = "Other group" },
                new TargetGroup { Id = 23, UserId = UserA, BotId = 3, GroupName = "Other bot group" });
            db.Leads.AddRange(new Lead { Id = 31, UserId = UserA, BotId = 1, ProfileName = "Own lead", ProfileUrl = "https://example.test/31" },
                new Lead { Id = 32, UserId = UserB, BotId = 1, ProfileName = "Other lead", ProfileUrl = "https://example.test/32" },
                new Lead { Id = 33, UserId = UserA, BotId = 3, ProfileName = "Second bot lead", ProfileUrl = "https://example.test/33" });
            db.BotActivityLogs.AddRange(new BotActivityLog { Id = 1, UserId = UserA, BotId = 1, CorrelationId = "owned-run", Message = "Own activity" },
                new BotActivityLog { Id = 2, UserId = UserB, BotId = 1, CorrelationId = "foreign-run", Message = "Other activity" });
            db.UserSettings.Add(new UserSetting { UserId = UserA, CompanyName = "Company", CompanyPitch = "Pitch",
                AIApiKeyExpirationDate = DateTime.UtcNow.AddDays(1), ScraperApiTokenExpirationDate = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
        });
    }

    private static ConnectedAccount Account(int id, Guid owner, int botId, string encrypted) => new()
    {
        Id = id, UserId = owner, BotId = botId, DisplayName = "Account " + id, Platform = "Facebook", Status = "Active",
        Cookie = new ConnectedAccountCookie { EncryptedCookies = encrypted, CookiesExpireDate = DateTime.UtcNow.AddDays(1) }
    };

    protected string AccessCookie(Guid id, string role = "User", string? session = null, DateTime? expires = null)
    {
        var token = new JwtSecurityToken(_security.JwtIssuer, _security.JwtAudience,
            [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role),
             new Claim("SessionToken", session ?? (id == UserB ? SessionB : SessionA))],
            expires: expires ?? DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_security.JwtSecret)), SecurityAlgorithms.HmacSha256));
        return $"{TokenService.AccessTokenCookieName}={new JwtSecurityTokenHandler().WriteToken(token)}";
    }

    protected async Task<HttpResponseMessage> Send(string method, string path, object? body = null, string? cookie = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (cookie != null) request.Headers.Add("Cookie", cookie);
        if (body != null) request.Content = JsonContent.Create(body);
        return await Client.SendAsync(request);
    }

    protected static string ResponseCookies(HttpResponseMessage response) => string.Join("; ",
        response.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));

    protected async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = _host.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected static async Task<JsonElement> SuccessData(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var root = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "data", "errorType", "isSuccess", "message" }, root.EnumerateObject().Select(value => value.Name).OrderBy(value => value));
        Assert.True(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(0, root.GetProperty("errorType").GetInt32());
        return root.GetProperty("data");
    }

    protected static async Task Error(HttpResponseMessage response, int status)
    {
        Assert.Equal(status, (int)response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.False(error!.IsSuccess); Assert.Equal(status, error.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId)); Assert.Null(error.Details); Assert.Null(error.InnerException);
    }

    public async Task DisposeAsync()
    {
        Client.Dispose(); await _host.StopAsync(); _host.Dispose(); await _connection.DisposeAsync();
    }

    private sealed class CaptureLoggerProvider(ConcurrentQueue<string> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(entries);
        public void Dispose() { }
        private sealed class CaptureLogger(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue(formatter(state, exception));
        }
    }
}
