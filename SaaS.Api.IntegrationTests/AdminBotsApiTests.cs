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
using SaaS.Domain.Constants;
using SaaS.Domain.Entities;
using SaaS.Infrastructure.Extensions;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Services;

namespace SaaS.Api.IntegrationTests;

// Tests real controllers, cookie authentication, admin checks, CQRS and relational constraints.
public sealed class AdminBotsApiTests : IAsyncLifetime
{
    private const string Route = "/api/v1/admin/bots";
    private const string Session = "admin-bots-user-session";
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _superAdminId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly SecuritySettings _security = new()
    {
        JwtSecret = "admin-bots-integration-signing-key-at-least-32-bytes",
        JwtIssuer = "admin-bots-tests", JwtAudience = "admin-bots-tests"
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
                    services.AddControllers().AddApplicationPart(typeof(AdminBotsController).Assembly);
                    services.AddJwtAuthentication(_security);
                    services.AddAuthorization();
                    services.AddMediatRWithValidation();
                    services.AddScoped<AppDbContext>(_ => new SqliteAppDbContext(
                        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options));
                    services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
                    services.AddScoped<IAdminBotPersistenceService, AdminBotPersistenceService>();
                    services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
                    services.AddScoped<IUserBotService, UserBotService>();
                })
                .Configure(app =>
                {
                    app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
                    app.UseMiddleware<ExceptionMiddleware>();
                    app.UseMiddleware<SingleActiveSessionMiddleware>();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                })).StartAsync();
        _client = _host.GetTestClient();
        await WithDb(async db =>
        {
            await db.Database.EnsureCreatedAsync();
            db.SystmeAdmins.AddRange(new SystemAdmin { Id = _adminId, Email = "admin@example.test", Role = "Admin" },
                new SystemAdmin { Id = _superAdminId, Email = "super@example.test", Role = "SuperAdmin" });
            db.Users.Add(new User { Id = _userId, Email = "user@example.test", CurrentSessionToken = Session });
            db.Bots.AddRange(new Bot { Id = 1, Name = "Known bot", UiModuleCode = "KNOWN-BOT", CurrentPrice = 12.50m,
                    IsActive = true, Status = "Active" },
                new Bot { Id = 2, Name = "Preview bot", UiModuleCode = "PREVIEW-BOT", CurrentPrice = 0m,
                    IsActive = false, Status = "Inactive" });
            // An existing use of a bot is seeded only to test that management preserves it.
            db.UserBots.Add(new UserBot { UserId = _userId, BotId = 1 });
            await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose(); await _host.StopAsync(); _host.Dispose(); await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task AdministrativeRoles_CanCreateReadUpdateActivateAndDeactivate(string role)
    {
        using var created = await Send(HttpMethod.Post, Route, new
        {
            name = " Future Bot ", description = "Preview module", currentPrice = 27.25m,
            uiModuleCode = " future-2027-bot ", cooldownMinutes = 0
        }, role);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var bot = await ReadBot(created);
        Assert.EndsWith($"{Route}/{bot.Id}", created.Headers.Location!.ToString());
        Assert.Equal("Future Bot", bot.Name); Assert.Equal("FUTURE-2027-BOT", bot.UiModuleCode);
        Assert.False(bot.IsActive); Assert.Equal("Inactive", bot.Status); Assert.Equal(0, bot.CooldownMinutes);
        Assert.Equal(27.25m, bot.CurrentPrice);
        Assert.Equal(bot, await ReadBot(await Send(HttpMethod.Get, $"{Route}/{bot.Id}", role: role)));
        var updated = await ReadBot(await Send(HttpMethod.Put, $"{Route}/{bot.Id}", new
        {
            name = "Updated bot", description = "Ready module", currentPrice = 29.99m,
            uiModuleCode = "future-2027-bot", cooldownMinutes = 60
        }, role));
        Assert.Equal("Updated bot", updated.Name); Assert.Equal("Ready module", updated.Description);
        Assert.Equal(29.99m, updated.CurrentPrice); Assert.Equal(60, updated.CooldownMinutes);
        Assert.False(updated.IsActive); Assert.Equal(bot.UiModuleCode, updated.UiModuleCode);
        var active = await ReadBot(await Send(HttpMethod.Put, $"{Route}/{bot.Id}/activate", role: role));
        Assert.True(active.IsActive); Assert.Equal("Active", active.Status);
        Assert.True((await ReadBot(await Send(HttpMethod.Put, $"{Route}/{bot.Id}/activate", role: role))).IsActive);
        var inactive = await ReadBot(await Send(HttpMethod.Put, $"{Route}/{bot.Id}/deactivate", role: role));
        Assert.False(inactive.IsActive); Assert.Equal("Inactive", inactive.Status);
        Assert.False((await ReadBot(await Send(HttpMethod.Put, $"{Route}/{bot.Id}/deactivate", role: role))).IsActive);
        await WithDb(async db =>
        {
            Assert.Equal(1, await db.UserBots.CountAsync());
            Assert.False((await db.Bots.FindAsync(bot.Id))!.IsActive);
            Assert.Equal(60, (await db.Bots.FindAsync(bot.Id))!.CooldownMinutes);
        });
    }

    [Fact]
    public async Task Create_DefaultsToInactiveAndUsesDefaultCooldown()
    {
        var bot = await ReadBot(await Send(HttpMethod.Post, Route,
            new { name = "Future AI", uiModuleCode = "NEW-AI-42", currentPrice = 0 }));
        Assert.False(bot.IsActive); Assert.Equal("Inactive", bot.Status); Assert.Equal(180, bot.CooldownMinutes);
        Assert.Null(bot.Description);
        await WithDb(async db =>
        {
            var saved = await db.Bots.FindAsync(bot.Id);
            Assert.False(saved!.IsActive); Assert.Equal(180, saved.CooldownMinutes);
            Assert.Empty(await db.UserBots.Where(subscription => subscription.BotId == bot.Id).ToListAsync());
        });
    }

    [Fact]
    public async Task Lifecycle_ControlsVisibilityInExistingUserCatalog()
    {
        var bot = await ReadBot(await Send(HttpMethod.Post, Route,
            new { name = "Future module", uiModuleCode = "FUTURE-CATALOG", currentPrice = 0 }));
        Assert.DoesNotContain(await UserCatalog(), item => item.Id == bot.Id);
        await ReadBot(await Send(HttpMethod.Put, $"{Route}/{bot.Id}/activate"));
        Assert.Contains(await UserCatalog(), item => item.Id == bot.Id && item.UiModuleCode == "FUTURE-CATALOG");
        await ReadBot(await Send(HttpMethod.Put, $"{Route}/{bot.Id}/deactivate"));
        Assert.DoesNotContain(await UserCatalog(), item => item.Id == bot.Id);
    }

    [Fact]
    public async Task List_SupportsPaginationSearchAndActiveFilters()
    {
        var first = await ReadPage(await Send(HttpMethod.Get, Route + "?pageSize=1"));
        Assert.Equal(1, Assert.Single(first.Items).Id); Assert.Equal(2, first.TotalItems); Assert.Equal(2, first.TotalPages);
        var second = await ReadPage(await Send(HttpMethod.Get, Route + "?pageSize=1&pageNumber=2"));
        Assert.Equal(2, Assert.Single(second.Items).Id); Assert.Equal(2, second.CurrentPage);
        Assert.Equal(1, Assert.Single((await ReadPage(await Send(HttpMethod.Get, Route + "?isActive=true"))).Items).Id);
        Assert.Equal(2, Assert.Single((await ReadPage(await Send(HttpMethod.Get, Route + "?isActive=false"))).Items).Id);
        Assert.Equal(2, Assert.Single((await ReadPage(await Send(HttpMethod.Get, Route + "?search=%20preview%20"))).Items).Id);
        Assert.Equal(1, Assert.Single((await ReadPage(await Send(HttpMethod.Get, Route + "?search=KNOWN-BOT"))).Items).Id);
        var empty = await ReadPage(await Send(HttpMethod.Get, Route + "?search=preview&isActive=true"));
        Assert.Empty(empty.Items); Assert.Equal(0, empty.TotalItems); Assert.Equal(0, empty.TotalPages);
        var beyond = await ReadPage(await Send(HttpMethod.Get, Route + "?pageNumber=3&pageSize=1"));
        Assert.Empty(beyond.Items); Assert.Equal(2, beyond.TotalItems); Assert.Equal(3, beyond.CurrentPage);
    }

    [Fact]
    public async Task Update_WithoutCodePreservesModuleAndLifecycleFields()
    {
        var bot = await ReadBot(await Send(HttpMethod.Put, Route + "/1", new { name = "Edited", currentPrice = 0, cooldownMinutes = 0 }));
        Assert.Equal("KNOWN-BOT", bot.UiModuleCode); Assert.True(bot.IsActive); Assert.Equal("Active", bot.Status);
        Assert.Equal(0, bot.CooldownMinutes); Assert.Equal(0m, bot.CurrentPrice); Assert.Null(bot.Description);
        await WithDb(async db =>
        {
            Assert.Equal(0, (await db.Bots.FindAsync(1))!.CooldownMinutes);
            Assert.Equal(1, Assert.Single(await db.UserBots.ToListAsync()).BotId);
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Update_CannotChangeCodeForUsedOrUnusedBotAndDoesNotWriteOtherFields(int id)
    {
        var original = await ReadBot(await Send(HttpMethod.Get, $"{Route}/{id}"));
        using var response = await Send(HttpMethod.Put, $"{Route}/{id}", new
            { name = "Must not change", uiModuleCode = "DIFFERENT-CODE", currentPrice = 99, cooldownMinutes = 1 });
        await AssertError(response, HttpStatusCode.Conflict);
        Assert.Equal(original, await ReadBot(await Send(HttpMethod.Get, $"{Route}/{id}")));
    }

    [Theory]
    [InlineData("KNOWN-BOT")]
    [InlineData("known-bot")]
    [InlineData(" KNOWN-BOT ")]
    [InlineData("preview-bot")]
    public async Task DuplicateCode_IncludingNormalizedAndInactiveBots_IsConflict(string code)
    {
        using var response = await Send(HttpMethod.Post, Route, new { name = "Duplicate", uiModuleCode = code, currentPrice = 0 });
        await AssertError(response, HttpStatusCode.Conflict);
        await WithDb(async db => Assert.Equal(2, await db.Bots.CountAsync()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-")]
    [InlineData("-NEW")]
    [InlineData("NEW-")]
    [InlineData("NEW--CODE")]
    [InlineData("NEW_CODE")]
    [InlineData("NEW CODE")]
    [InlineData("<SCRIPT>")]
    [InlineData("../CODE")]
    [InlineData("مرحبا")]
    public async Task InvalidOrEmptyCode_IsRejectedWithoutCreatingBot(string? code)
    {
        using var response = await Send(HttpMethod.Post, Route, new { name = "Invalid", uiModuleCode = code, currentPrice = 0 });
        await AssertError(response, HttpStatusCode.BadRequest);
        await WithDb(async db => Assert.Equal(2, await db.Bots.CountAsync()));
    }

    [Fact]
    public async Task ModuleCode_LengthBoundaryAndDynamicHelperAcceptFutureCodes()
    {
        Assert.True(UiModuleCodes.IsValid("future-integration-2030"));
        Assert.True(UiModuleCodes.IsValid(UiModuleCodes.FbMembersScraper));
        var bot = await ReadBot(await Send(HttpMethod.Post, Route,
            new { name = "Boundary", uiModuleCode = new string('A', 50), currentPrice = 0 }));
        Assert.Equal(50, bot.UiModuleCode.Length);
        using var response = await Send(HttpMethod.Post, Route,
            new { name = "Too long", uiModuleCode = new string('A', 51), currentPrice = 0 });
        await AssertError(response, HttpStatusCode.BadRequest);
    }

    public static IEnumerable<object[]> BlockedRequests()
    {
        foreach (var operation in new[] { "list", "detail", "create", "update", "activate", "deactivate" })
        foreach (var role in new[] { "Anonymous", "User", "Support", "Worker", "SystemAdmin" })
            yield return [operation, role];
    }

    [Theory]
    [MemberData(nameof(BlockedRequests))]
    public async Task AllEndpoints_RejectAnonymousAndNonAdministrativeRoles(string operation, string role)
    {
        var (method, route, body) = RequestFor(operation, 1);
        using var response = await Send(method, route, body, role == "Anonymous" ? null : role);
        Assert.Equal(role == "Anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
        await WithDb(async db =>
        {
            Assert.Equal(2, await db.Bots.CountAsync());
            Assert.True((await db.Bots.FindAsync(1))!.IsActive);
            Assert.Equal("Known bot", (await db.Bots.FindAsync(1))!.Name);
        });
    }

    [Theory]
    [InlineData("inactive", 401)]
    [InlineData("missing", 401)]
    [InlineData("empty-id", 401)]
    [InlineData("changed-role", 403)]
    public async Task StaleAdminIdentityOrRole_IsRejected(string state, int expectedStatus)
    {
        var actorId = state == "missing" ? Guid.NewGuid() : state == "empty-id" ? Guid.Empty : _adminId;
        await WithDb(async db =>
        {
            var admin = await db.SystmeAdmins.FindAsync(_adminId);
            if (state == "inactive") admin!.IsActive = false;
            if (state == "changed-role") admin!.Role = "Support";
            await db.SaveChangesAsync();
        });
        using var response = await Send(HttpMethod.Post, Route,
            new { name = "Denied", uiModuleCode = "DENIED-BOT", currentPrice = 0 }, actorId: actorId);
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        await WithDb(async db => Assert.Equal(2, await db.Bots.CountAsync()));
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("update")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task MissingBot_ReturnsStandardNotFound(string operation)
    {
        var (method, route, body) = RequestFor(operation, 999);
        using var response = await Send(method, route, body);
        await AssertError(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":\"Bot\",\"currentPrice\":0}")]
    [InlineData("{\"name\":\"Bot\",\"uiModuleCode\":\"CODE\"}")]
    [InlineData("{\"name\":\" \",\"uiModuleCode\":\"CODE\",\"currentPrice\":0}")]
    [InlineData("{\"name\":\"Bot\",\"uiModuleCode\":\"CODE\",\"currentPrice\":-1}")]
    [InlineData("{\"name\":\"Bot\",\"uiModuleCode\":\"CODE\",\"currentPrice\":1.001}")]
    [InlineData("{\"name\":\"Bot\",\"uiModuleCode\":\"CODE\",\"currentPrice\":10000000000000000}")]
    [InlineData("{\"name\":\"Bot\",\"uiModuleCode\":\"CODE\",\"currentPrice\":0,\"cooldownMinutes\":-1}")]
    [InlineData("{\"name\":\"Bot\",\"uiModuleCode\":\"CODE\",\"currentPrice\":0,\"cooldownMinutes\":1.5}")]
    [InlineData("{\"name\":\"Bot\",\"uiModuleCode\":\"CODE\",\"currentPrice\":0,\"isActive\":true}")]
    [InlineData("{\"name\":\"Bot\",\"uiModuleCode\":\"CODE\",\"currentPrice\":0,\"status\":\"Active\"}")]
    [InlineData("{\"name\":\"Bot\",\"uiModuleCode\":\"CODE\",\"currentPrice\":0,\"userBots\":[]}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task InvalidCreateBody_ReturnsBadRequestWithoutWriting(string json)
    {
        using var response = await Send(HttpMethod.Post, Route, rawJson: json);
        await AssertError(response, HttpStatusCode.BadRequest);
        await WithDb(async db => Assert.Equal(2, await db.Bots.CountAsync()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":\"Bot\",\"currentPrice\":0,\"uiModuleCode\":\"\"}")]
    [InlineData("{\"name\":\"Bot\",\"currentPrice\":0,\"uiModuleCode\":\"BAD_CODE\"}")]
    [InlineData("{\"name\":\"Bot\",\"currentPrice\":0,\"isActive\":false}")]
    [InlineData("{\"name\":\"Bot\",\"currentPrice\":0,\"status\":\"Inactive\"}")]
    public async Task InvalidUpdateBody_CannotAlterCodeOrLifecycle(string json)
    {
        using var response = await Send(HttpMethod.Put, Route + "/1", rawJson: json);
        await AssertError(response, HttpStatusCode.BadRequest);
        await WithDb(async db => Assert.Equal("Known bot", (await db.Bots.FindAsync(1))!.Name));
    }

    [Theory]
    [InlineData("?pageNumber=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageNumber=2147483647&pageSize=100")]
    [InlineData("?isActive=invalid")]
    [InlineData("?pageSize=invalid")]
    public async Task InvalidListFilters_ReturnStandardBadRequest(string query)
    {
        using var response = await Send(HttpMethod.Get, Route + query);
        await AssertError(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidNameAndBotId_AreRejected()
    {
        using var name = await Send(HttpMethod.Post, Route,
            new { name = new string('x', 101), uiModuleCode = "LONG-NAME", currentPrice = 0 });
        await AssertError(name, HttpStatusCode.BadRequest);
        using var id = await Send(HttpMethod.Get, Route + "/0");
        await AssertError(id, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DatabaseUniqueIndex_PreventsDuplicateEvenWithoutApiPrecheck()
    {
        await WithDb(async db =>
        {
            db.Bots.Add(new Bot { Name = "Duplicate", UiModuleCode = "KNOWN-BOT" });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(19, Assert.IsType<SqliteException>(error.InnerException).SqliteErrorCode);
        });
        await WithDb(async db => Assert.Equal(2, await db.Bots.CountAsync()));
    }

    [Fact]
    public void SqlServerMigration_AddsUniqueCodeIndexWithoutChangingExistingCodes()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var bot = db.Model.FindEntityType(typeof(Bot))!;
        var index = Assert.Single(bot.GetIndexes(), item => item.GetDatabaseName() == "IX_Bots_UiModuleCode");
        Assert.True(index.IsUnique);
        var migration = db.GetService<IMigrationsAssembly>().Migrations.Keys.Single(key => key.EndsWith("AddUniqueBotUiModuleCode"));
        var sql = db.GetService<IMigrator>().GenerateScript("20261013212118_initialMigration", migration);
        Assert.Contains("CREATE UNIQUE INDEX [IX_Bots_UiModuleCode] ON [Bots] ([UiModuleCode])", sql);
        Assert.DoesNotContain("UPDATE [Bots]", sql);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private (HttpMethod Method, string Route, object? Body) RequestFor(string operation, int id) => operation switch
    {
        "list" => (HttpMethod.Get, Route, null),
        "detail" => (HttpMethod.Get, $"{Route}/{id}", null),
        "create" => (HttpMethod.Post, Route, new { name = "Created", uiModuleCode = "CREATED-BOT", currentPrice = 0 }),
        "update" => (HttpMethod.Put, $"{Route}/{id}", new { name = "Updated", currentPrice = 0 }),
        _ => (HttpMethod.Put, $"{Route}/{id}/{operation}", null)
    };

    private async Task<List<BotDto>> UserCatalog()
    {
        using var response = await Send(HttpMethod.Get, "/api/v1/bots", role: "User");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<List<BotDto>>>())!.Data!;
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string route, object? body = null,
        string? role = "Admin", Guid? actorId = null, string? rawJson = null)
    {
        using var request = new HttpRequestMessage(method, route);
        if (role is not null)
        {
            var id = actorId ?? (role == "SuperAdmin" ? _superAdminId : role == "User" ? _userId : _adminId);
            var token = new JwtSecurityToken(_security.JwtIssuer, _security.JwtAudience,
                [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role), new Claim("SessionToken", Session)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_security.JwtSecret)), SecurityAlgorithms.HmacSha256));
            request.Headers.Add("Cookie", $"{TokenService.AccessTokenCookieName}={new JwtSecurityTokenHandler().WriteToken(token)}");
        }
        if (rawJson is not null) request.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        else if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static async Task<AdminBotDto> ReadBot(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            AssertPublicFields(json.RootElement.GetProperty("data"));
            return (await response.Content.ReadFromJsonAsync<ApiResponse<AdminBotDto>>())!.Data!;
        }
    }

    private static async Task<PaginatedResult<AdminBotDto>> ReadPage(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var bot in json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
                AssertPublicFields(bot);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<PaginatedResult<AdminBotDto>>>())!.Data!;
        }
    }

    private static void AssertPublicFields(JsonElement bot) => Assert.Equal(
        new[] { "cooldownMinutes", "currentPrice", "description", "id", "isActive", "name", "status", "uiModuleCode" },
        bot.EnumerateObject().Select(property => property.Name).Order());

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.False(error!.IsSuccess); Assert.Equal((int)status, error.StatusCode);
        Assert.False(string.IsNullOrEmpty(error.Path)); Assert.False(string.IsNullOrEmpty(error.TraceId));
        Assert.Null(error.Details);
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = _host.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
