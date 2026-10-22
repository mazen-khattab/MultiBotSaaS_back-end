using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SaaS.Api.Controllers.v1;
using SaaS.Api.Middlewares;
using SaaS.Application.Common.Dtos;
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

public sealed class ScrapesApiTests : IAsyncLifetime
{
    private const string Route = "/api/v1/scrapes";
    private const string Session = "scrapes-test-session";
    private const string WebhookSecret = "scrapes-test-webhook-secret";
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Mock<INetworkClient> _network = new();
    private readonly Mock<IAppNotificationService> _notifications = new();
    private readonly SecuritySettings _security = new()
    {
        JwtSecret = "scrapes-integration-signing-key-at-least-32-bytes",
        JwtIssuer = "scrapes-tests", JwtAudience = "scrapes-tests",
        EncryptionKey = "scrapes-integration-encryption-key"
    };
    private IHost _host = null!;
    private HttpClient _client = null!;
    private SqliteConnection _connection = null!;
    private object? _dispatchPayload;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _network.Setup(network => network.PostJsonAsync(It.IsAny<string>(), It.IsAny<object>(),
                ExternalSystem.N8n, It.IsAny<CancellationToken>()))
            .Callback<string, object, ExternalSystem, CancellationToken>((_, payload, _, _) => _dispatchPayload = payload)
            .ReturnsAsync(NetworkResult.Ok(200));
        var resolver = new Mock<IN8nWebhookResolver>();
        resolver.Setup(value => value.GetWebhookUrl(It.IsAny<string>())).Returns("https://example.test/scrapes");
        _host = await new HostBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHost(web => web.UseTestServer().UseEnvironment("Production")
                .ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(ScrapesController).Assembly);
                    services.AddJwtAuthentication(_security);
                    services.AddAuthorization();
                    services.AddMediatRWithValidation();
                    services.AddScoped<AppDbContext>(_ => new SqliteAppDbContext(
                        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options));
                    services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
                    services.Configure<SecuritySettings>(options => options.EncryptionKey = _security.EncryptionKey);
                    services.Configure<N8nOptions>(options => options.N8nSecret = WebhookSecret);
                    services.AddSingleton<IEncryptionService, EncryptionService>();
                    services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
                    services.AddScoped<IUserBotService, UserBotService>();
                    services.AddSingleton(_network.Object);
                    services.AddSingleton(_notifications.Object);
                    services.AddSingleton(resolver.Object);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseMiddleware<ExceptionMiddleware>();
                    app.UseMiddleware<SingleActiveSessionMiddleware>();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .StartAsync();
        _client = _host.GetTestClient();
        await WithDb(async db =>
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(new User { Id = _userId, Email = "owner@example.test", CurrentSessionToken = Session },
                new User { Id = _otherUserId, Email = "other@example.test", CurrentSessionToken = Session });
            db.Bots.Add(new Bot { Id = 100, Name = "Test scraper", UiModuleCode = "TEST-SCRAPER" });
            db.UserBots.Add(new UserBot { UserId = _userId, BotId = 100 });
            db.ConnectedAccounts.Add(new ConnectedAccount
            {
                Id = 100, UserId = _userId, BotId = 100, DisplayName = "Scraper account",
                Platform = "Facebook", Status = "Active", Cookie = new ConnectedAccountCookie
                {
                    AccountId = 100,
                    EncryptedCookies = _host.Services.GetRequiredService<IEncryptionService>().Encrypt("test-cookies"),
                    CookiesExpireDate = DateTime.UtcNow.AddDays(1)
                }
            });
            db.UserSettings.Add(new UserSetting { UserId = _userId, CompanyName = "Company", CompanyPitch = "Pitch" });
            await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
        await _connection.DisposeAsync();
    }

    [Fact]
    public void SqlServerModel_UsesScrapesTableAndRenamedKeysColumnsIndexesAndNavigations()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);
        var entity = db.Model.FindEntityType(typeof(Scrape))!;
        Assert.Equal("Scrapes", entity.GetTableName());
        Assert.Equal("PK_Scrapes", entity.FindPrimaryKey()!.GetName());
        Assert.All(entity.GetForeignKeys(), key => Assert.StartsWith("FK_Scrapes_", key.GetConstraintName()!));
        var lead = db.Model.FindEntityType(typeof(Lead))!;
        Assert.Null(lead.FindProperty("RunId"));
        Assert.NotNull(lead.FindProperty(nameof(Lead.ScrapeId)));
        var foreignKey = Assert.Single(lead.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Scrape));
        Assert.Equal("FK_Leads_Scrapes_ScrapeId", foreignKey.GetConstraintName());
        Assert.Contains(lead.GetIndexes(), index => index.GetDatabaseName() == "IX_Leads_UserId_ScrapeId_Status");
        foreach (var owner in new[] { typeof(User), typeof(Bot), typeof(TargetGroup), typeof(ConnectedAccount) })
            Assert.NotNull(db.Model.FindEntityType(owner)!.FindNavigation("Scrapes"));
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE [Scrapes]", sql);
        Assert.DoesNotContain("[Runs]", sql);
        Assert.DoesNotContain("[RunId]", sql);
    }

    [Fact]
    public void MigrationScript_CreatesSettingsBeforeReferencingExpirationColumns()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);
        var script = db.GetService<IMigrator>().GenerateScript();
        var createSettingsIndex = script.IndexOf("CREATE TABLE [UserSettings]", StringComparison.Ordinal);
        Assert.True(createSettingsIndex >= 0);
        foreach (var column in new[] { "AIApiKeyExpirationDate", "ScraperApiTokenExpirationDate" })
        {
            var firstColumnIndex = script.IndexOf($"[{column}]", StringComparison.Ordinal);
            Assert.True(firstColumnIndex > createSettingsIndex,
                $"The initial schema must create UserSettings before referencing {column}.");
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Create_WithExpiredApiCredential_DoesNotCreateScrapeOrDispatch(bool aiExpired, bool scraperExpired)
    {
        await WithDb(async db =>
        {
            var settings = await db.UserSettings.SingleAsync(setting => setting.UserId == _userId);
            settings.AIApiKeyExpirationDate = DateTime.UtcNow.AddDays(aiExpired ? -1 : 1);
            settings.ScraperApiTokenExpirationDate = DateTime.UtcNow.AddDays(scraperExpired ? -1 : 1);
            await db.SaveChangesAsync();
        });

        using var response = await Send(HttpMethod.Post, Route, _userId, new CreateScrapeDto(100, 100, null, "{}"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("API Keys have expired. Please renew them.", error!.Message);
        await WithDb(async db =>
        {
            Assert.Empty(await db.Scrapes.ToListAsync());
            Assert.Equal("Active", (await db.ConnectedAccounts.SingleAsync()).Status);
        });
        _network.Verify(network => network.PostJsonAsync(It.IsAny<string>(), It.IsAny<object>(),
            ExternalSystem.N8n, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_DispatchesScrapeIdAndPersistsScrapeWithAccountNavigation()
    {
        using var response = await Send(HttpMethod.Post, Route, _userId,
            new CreateScrapeDto(100, 100, null, "{}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        Assert.True(result!.IsSuccess);
        Assert.Equal("Scrape started successfully.", result.Message);
        var payload = JsonSerializer.SerializeToElement(_dispatchPayload);
        Assert.Equal(result.Data, payload.GetProperty("ScrapeId").GetInt32());
        Assert.False(payload.TryGetProperty("RunId", out _));
        await WithDb(async db =>
        {
            var scrape = Assert.Single(await db.Scrapes.ToListAsync());
            Assert.Equal(result.Data, scrape.Id);
            Assert.Equal("Running", scrape.Status);
            var account = await db.ConnectedAccounts.Include(account => account.Scrapes).SingleAsync();
            Assert.Equal("Busy", account.Status);
            Assert.Equal(scrape.Id, Assert.Single(account.Scrapes).Id);
        });
    }

    [Fact]
    public async Task ListAndDetails_AreScopedToAuthenticatedUser()
    {
        await SeedScrapes();
        using var list = await Send(HttpMethod.Get, $"{Route}?botId=100", _userId);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content.ReadFromJsonAsync<ApiResponse<PaginatedResult<ScrapeDto>>>())!.Data!;
        Assert.Equal(1, page.TotalItems);
        Assert.Equal(101, Assert.Single(page.Items).Id);
        using var own = await Send(HttpMethod.Get, $"{Route}/101", _userId);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var other = await Send(HttpMethod.Get, $"{Route}/102", _userId);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task Cancel_UpdatesScrapeAndCannotModifyOtherUserScrape()
    {
        await SeedScrapes();
        using var other = await Send(HttpMethod.Post, $"{Route}/102/cancel", _userId);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using var own = await Send(HttpMethod.Post, $"{Route}/101/cancel", _userId);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        await WithDb(async db =>
        {
            Assert.Equal("Cancelled", (await db.Scrapes.SingleAsync(scrape => scrape.Id == 101)).Status);
            Assert.Equal("Running", (await db.Scrapes.SingleAsync(scrape => scrape.Id == 102)).Status);
        });
    }

    [Fact]
    public async Task Complete_WithWebhookSecret_PersistsLeadDetailsAndNotifiesCompletion()
    {
        await SeedScrapes();
        const string metadata = "{\"bio\":\"Consultant\",\"comments\":[\"Interested\"]}";
        using var response = await Send(HttpMethod.Post, $"{Route}/101/complete", null,
            new CompleteScrapeDto([
                new ScrapedLeadDto("profile-123", "lead", "Test Lead", "Hello", metadata),
                new ScrapedLeadDto("profile-456", "other-lead", "Other Lead", "Hi", "{}")
            ]), WebhookSecret);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithDb(async db =>
        {
            var leads = await db.Leads.Include(lead => lead.Scrape).ToListAsync();
            var details = await db.LeadDetails.AsNoTracking().ToListAsync();
            Assert.Equal(2, leads.Count);
            Assert.Equal(2, details.Count);
            foreach (var lead in leads)
            {
                Assert.Equal(101, lead.ScrapeId);
                Assert.Equal("Completed", lead.Scrape!.Status);
                var detail = Assert.Single(details, candidate => candidate.LeadId == lead.Id);
                Assert.Equal(lead.ExternalId == "profile-123" ? metadata : "{}", detail.MetaDataJson);
            }
        });
        _notifications.Verify(service => service.NotifyScrapeCompletedAsync(_userId, 101, 2), Times.Once);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/101")]
    [InlineData("POST", "")]
    [InlineData("POST", "/101/cancel")]
    [InlineData("POST", "/101/complete")]
    public async Task ProtectedRoutes_RejectUnauthenticatedRequests(string method, string suffix)
    {
        using var response = await Send(new HttpMethod(method), Route + suffix, null, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task SeedScrapes() => WithDb(async db =>
    {
        db.Scrapes.AddRange(new Scrape { Id = 101, UserId = _userId, BotId = 100, Status = "Running" },
            new Scrape { Id = 102, UserId = _otherUserId, BotId = 100, Status = "Running" });
        await db.SaveChangesAsync();
    });

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, Guid? userId,
        object? body = null, string? webhookSecret = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (userId.HasValue)
        {
            var token = new JwtSecurityToken(_security.JwtIssuer, _security.JwtAudience,
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()), new Claim(ClaimTypes.Role, "User"),
                 new Claim("SessionToken", Session)], expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_security.JwtSecret)),
                    SecurityAlgorithms.HmacSha256));
            request.Headers.Add("Cookie", $"{TokenService.AccessTokenCookieName}={new JwtSecurityTokenHandler().WriteToken(token)}");
        }
        if (webhookSecret is not null)
            request.Headers.Add("X-Webhook-Secret", webhookSecret);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = _host.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
