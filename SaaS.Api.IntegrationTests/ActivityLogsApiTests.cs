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
using SaaS.Api.Controllers.v1;
using SaaS.Api.Middlewares;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Common.Services;
using SaaS.Application.Common.Settings;
using SaaS.Domain.Entities;
using SaaS.Infrastructure.Extensions;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Services;

namespace SaaS.Api.IntegrationTests;

// Uses production controllers, JWT/session checks, CQRS and a relational EF model.
public sealed class ActivityLogsApiTests : IAsyncLifetime
{
    private const string Route = "/api/v1/activity-logs";
    private const string Session = "activity-logs-session";
    private const string WorkerSecret = "activity-logs-worker-secret";
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly SecuritySettings _security = new()
    {
        JwtSecret = "activity-logs-integration-signing-key-at-least-32-bytes",
        JwtIssuer = "activity-logs-tests", JwtAudience = "activity-logs-tests"
    };
    private SqliteConnection _connection = null!;
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _host = await new HostBuilder().ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHost(web => web.UseTestServer().UseEnvironment("Production")
                .ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(ActivityLogsController).Assembly);
                    services.AddJwtAuthentication(_security);
                    services.AddAuthorization();
                    services.AddMediatRWithValidation();
                    services.AddScoped<AppDbContext>(_ => new SqliteAppDbContext(
                        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options));
                    services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
                    services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
                    services.AddScoped<IUserBotService, UserBotService>();
                    services.Configure<WorkerOptions>(options => options.WorkerSecret = WorkerSecret);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseMiddleware<ExceptionMiddleware>();
                    app.UseMiddleware<SingleActiveSessionMiddleware>();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                })).StartAsync();
        _client = _host.GetTestClient();
        await WithDb(async db =>
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(new User { Id = _userId, Email = "owner@example.test", CurrentSessionToken = Session },
                new User { Id = _otherUserId, Email = "other@example.test", CurrentSessionToken = Session });
            db.Bots.AddRange(new Bot { Id = 1, Name = "Shared bot", UiModuleCode = "SHARED" },
                new Bot { Id = 2, Name = "Second bot", UiModuleCode = "SECOND" },
                new Bot { Id = 3, Name = "Other user's bot", UiModuleCode = "OTHER" });
            db.UserBots.AddRange(new UserBot { UserId = _userId, BotId = 1 },
                new UserBot { UserId = _otherUserId, BotId = 1 },
                new UserBot { UserId = _userId, BotId = 2 },
                new UserBot { UserId = _otherUserId, BotId = 3 });
            var time = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
            db.BotActivityLogs.AddRange(
                Log(1, _userId, 1, "shared-correlation", "INFO", time),
                Log(2, _userId, 1, "shared-correlation", "ERROR", time),
                Log(3, _userId, 2, "second-correlation", "WARN", time.AddMinutes(1)),
                Log(4, _userId, null, "legacy-correlation", "INFO", time.AddMinutes(2)),
                Log(5, _otherUserId, 1, "shared-correlation", "ERROR", time.AddMinutes(3)),
                Log(6, _otherUserId, 3, "other-correlation", "INFO", time.AddMinutes(3)),
                Log(7, null, null, "system-correlation", "INFO", time.AddMinutes(3)),
                // A mismatched association must not leak, even on an unfiltered request.
                Log(8, _userId, 3, "invalid-association", "INFO", time.AddMinutes(3)));
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
    public async Task Pagination_CountsOnlyVisibleLogsAndOrdersTiesById()
    {
        var first = await GetPage("?pageSize=3");
        Assert.Equal(new long[] { 4, 3, 2 }, first.Items.Select(log => log.Id));
        Assert.Equal(4, first.TotalItems);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(1, first.CurrentPage);
        var second = await GetPage("?pageNumber=2&pageSize=3");
        Assert.Equal(1, Assert.Single(second.Items).Id);
        Assert.Equal(2, second.CurrentPage);
        Assert.Equal(4, second.TotalItems);
        Assert.Empty((await GetPage("?pageNumber=3&pageSize=3")).Items);
    }

    [Fact]
    public async Task BotFilter_ExcludesOtherBotsLegacyAndOtherOwnersOnSharedBot()
    {
        var page = await GetPage("?botId=1");
        Assert.Equal(new long[] { 2, 1 }, page.Items.Select(log => log.Id));
        Assert.Equal(2, page.TotalItems);
        Assert.Equal(3, Assert.Single((await GetPage("?botId=2")).Items).Id);
    }

    [Fact]
    public async Task RunFilter_UsesExactCorrelationAndIsolatesSharedCorrelations()
    {
        var page = await GetPage("?runId=shared-correlation");
        Assert.Equal(new long[] { 2, 1 }, page.Items.Select(log => log.Id));
        Assert.Equal(2, page.TotalItems);
        Assert.Equal(4, Assert.Single((await GetPage("?runId=legacy-correlation")).Items).Id);
    }

    [Theory]
    [InlineData("INFO", 2)]
    [InlineData("WARN", 1)]
    [InlineData("ERROR", 1)]
    public async Task LogLevelFilter_ReturnsOnlyMatchingOwnedLogs(string level, int count)
    {
        var page = await GetPage($"?logLevel={level}");
        Assert.Equal(count, page.TotalItems);
        Assert.All(page.Items, log => Assert.Equal(level, log.LogLevel));
    }

    [Fact]
    public async Task CombinedFilters_ApplyBeforePaginationAndEmptyMatchesAreSuccessful()
    {
        var page = await GetPage("?botId=1&runId=shared-correlation&logLevel=ERROR&pageSize=1");
        Assert.Equal(2, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.TotalItems);
        Assert.Empty((await GetPage("?botId=1&runId=shared-correlation&logLevel=WARN")).Items);
        Assert.Empty((await GetPage("?botId=2&runId=shared-correlation")).Items);
    }

    [Fact]
    public async Task SuppliedUserId_IsIgnoredAndCannotSelectAnotherOwner()
    {
        var page = await GetPage($"?userId={_otherUserId}");
        Assert.Equal(new long[] { 4, 3, 2, 1 }, page.Items.Select(log => log.Id));
        using var response = await Send("?botId=3&userId=" + _otherUserId, _userId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("?botId=3")]
    [InlineData("?botId=999")]
    [InlineData("?runId=other-correlation")]
    [InlineData("?runId=system-correlation")]
    [InlineData("?runId=missing")]
    [InlineData("?runId=invalid-association")]
    public async Task UnownedOrMissingResources_ReturnStandardNotFound(string query)
    {
        using var response = await Send(query, _userId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.False(error!.IsSuccess);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal(Route, error.Path);
        Assert.Equal("GET", error.Method);
        Assert.False(string.IsNullOrEmpty(error.TraceId));
    }

    [Fact]
    public async Task UnfilteredRead_AlsoRechecksCurrentBotOwnership()
    {
        await WithDb(async db =>
        {
            db.UserBots.Remove(await db.UserBots.SingleAsync(item => item.UserId == _userId && item.BotId == 1));
            await db.SaveChangesAsync();
        });
        Assert.Equal(new long[] { 4, 3 }, (await GetPage("")).Items.Select(log => log.Id));
    }

    [Fact]
    public async Task ProductionResponse_ContainsOnlyPublicDtoFieldsAndDoesNotExposeStackTraces()
    {
        using var response = await Send("?logLevel=ERROR", _userId);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("internal-secret", json);
        Assert.DoesNotContain("stackTrace", json, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        var item = document.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(new[] { "correlationId", "createdAt", "id", "logLevel", "message" },
            item.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("Log 2", item.GetProperty("message").GetString());
        await WithDb(async db => Assert.Contains("internal-secret", (await db.BotActivityLogs.FindAsync(2L))!.StackTrace!));
    }

    [Fact]
    public async Task AnonymousAndInvalidCookie_AreUnauthorized()
    {
        using var anonymous = await Send("", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add("Cookie", $"{TokenService.AccessTokenCookieName}=invalid-token");
        using var invalid = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    [InlineData("Worker")]
    public async Task NonUserRoles_AreForbidden(string role)
    {
        using var response = await Send("", _userId, role);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidatedSessionAndEmptyIdentity_AreUnauthorized()
    {
        using var expired = await Send("", _userId, session: "old-session");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var emptyIdentity = await Send("", Guid.Empty);
        Assert.Equal(HttpStatusCode.Unauthorized, emptyIdentity.StatusCode);
    }

    [Theory]
    [InlineData("?pageNumber=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageNumber=2147483647&pageSize=100")]
    [InlineData("?botId=-1")]
    [InlineData("?logLevel=DEBUG")]
    [InlineData("?logLevel=error")]
    [InlineData("?logLevel=")]
    [InlineData("?runId=")]
    [InlineData("?runId=%20")]
    public async Task InvalidFilters_ReturnValidationError(string query)
    {
        using var response = await Send(query, _userId);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(400, error!.StatusCode);
        Assert.Null(error.Details);
    }

    [Theory]
    [InlineData("?botId=not-a-number")]
    [InlineData("?pageNumber=2147483648")]
    [InlineData("?pageSize=1.5")]
    public async Task InvalidNumericBinding_ReturnsBadRequest(string query)
    {
        using var response = await Send(query, _userId);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OversizedCorrelation_IsRejected()
    {
        using var response = await Send("?runId=" + new string('x', 101), _userId);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserWithoutLogs_ReturnsEmptyPage()
    {
        await WithDb(async db =>
        {
            db.BotActivityLogs.RemoveRange(await db.BotActivityLogs.Where(log => log.UserId == _userId).ToListAsync());
            await db.SaveChangesAsync();
        });
        var page = await GetPage("");
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalItems);
        Assert.Equal(0, page.TotalPages);
        Assert.Equal(1, page.CurrentPage);
    }

    [Fact]
    public async Task WorkerWrites_RemainCompatibleAndCanAssociateLogsToOwnedBot()
    {
        using var legacy = await WriteLog(null);
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        using var associated = await WriteLog(1);
        Assert.Equal(HttpStatusCode.OK, associated.StatusCode);
        var page = await GetPage("?botId=1&runId=new-worker-correlation");
        Assert.Equal("Worker log", Assert.Single(page.Items).Message);
        Assert.Equal(2, (await GetPage("?runId=new-worker-correlation")).TotalItems);
        using var rejected = await WriteLog(3);
        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        Assert.Equal(2, (await GetPage("?runId=new-worker-correlation")).TotalItems);
    }

    [Fact]
    public void SqlServerMigration_IncludesNullableBotAssociationAndMatchesCurrentModel()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True").Options);
        // The activity-log association is now part of the consolidated initial migration.
        var sql = db.GetService<IMigrator>().GenerateScript();
        var logTableStart = sql.IndexOf("CREATE TABLE [BotActivityLogs]", StringComparison.Ordinal);
        Assert.True(logTableStart >= 0);
        var logTableSql = sql[logTableStart..sql.IndexOf("GO", logTableStart, StringComparison.Ordinal)];
        Assert.Contains("[BotId] int NULL", logTableSql);
        Assert.Contains("FOREIGN KEY ([BotId]) REFERENCES [Bots] ([Id]) ON DELETE SET NULL", logTableSql);
        Assert.DoesNotContain("UPDATE [BotActivityLogs]", sql);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static BotActivityLog Log(long id, Guid? userId, int? botId, string correlation, string level, DateTime time) =>
        new() { Id = id, UserId = userId, BotId = botId, CorrelationId = correlation, LogLevel = level,
            Message = $"Log {id}", StackTrace = "internal-secret at Worker.Run()", CreatedAt = time };

    private async Task<PaginatedResult<BotActivityLogDto>> GetPage(string query)
    {
        using var response = await Send(query, _userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PaginatedResult<BotActivityLogDto>>>();
        Assert.True(result!.IsSuccess);
        Assert.Equal("Activity logs retrieved successfully.", result.Message);
        return result.Data!;
    }

    private async Task<HttpResponseMessage> WriteLog(int? botId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/worker/activity-log");
        request.Headers.Add("x-worker-api-key", WorkerSecret);
        request.Content = JsonContent.Create(new { userId = _userId, botId,
            correlationId = "new-worker-correlation", logLevel = "INFO", message = "Worker log", stackTrace = "internal-secret" });
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Send(string query, Guid? userId, string role = "User", string session = Session)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Route + query);
        if (userId.HasValue)
        {
            var token = new JwtSecurityToken(_security.JwtIssuer, _security.JwtAudience,
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                    new Claim(ClaimTypes.Role, role), new Claim("SessionToken", session)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_security.JwtSecret)), SecurityAlgorithms.HmacSha256));
            request.Headers.Add("Cookie", $"{TokenService.AccessTokenCookieName}={new JwtSecurityTokenHandler().WriteToken(token)}");
        }
        return await _client.SendAsync(request);
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = _host.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
