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

// Exercises HTTP authorization, CQRS, relational uniqueness and the existing ownership queries.
public sealed class AdminSubscriptionsApiTests : IAsyncLifetime
{
    private const string Route = "/api/v1/admin/subscriptions";
    private const string Session = "subscription-session-secret";
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _superAdminId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _deletedUserId = Guid.NewGuid();
    private readonly DateTime _legacyDate = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly SecuritySettings _security = new()
    {
        JwtSecret = "admin-subscriptions-integration-signing-key-at-least-32-bytes",
        JwtIssuer = "admin-subscriptions-tests", JwtAudience = "admin-subscriptions-tests"
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
                    services.AddControllers().AddApplicationPart(typeof(AdminSubscriptionsController).Assembly);
                    services.AddJwtAuthentication(_security);
                    services.AddAuthorization();
                    services.AddMediatRWithValidation();
                    services.AddScoped<AppDbContext>(_ => new SqliteAppDbContext(
                        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options));
                    services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
                    services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
                    services.AddScoped<IUserBotService, UserBotService>();
                    services.AddScoped<IAdminSubscriptionPersistenceService, AdminSubscriptionPersistenceService>();
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
            db.Users.AddRange(new User { Id = _userId, FullName = "Owner", Email = "owner@example.test",
                    PasswordHash = "password-hash-secret", CurrentSessionToken = Session, LastLoginIp = "internal-ip-secret" },
                new User { Id = _otherUserId, FullName = "Other user", Email = "other@example.test", CurrentSessionToken = Session },
                new User { Id = _deletedUserId, FullName = "Deactivated user", Email = "deleted@example.test", IsDeleted = true });
            db.Bots.AddRange(new Bot { Id = 1, Name = "Existing bot", UiModuleCode = "EXISTING-BOT", CurrentPrice = 199.99m },
                new Bot { Id = 2, Name = "Preview bot", UiModuleCode = "PREVIEW-BOT", CurrentPrice = 299.99m,
                    IsActive = false, Status = "Inactive" },
                new Bot { Id = 3, Name = "New bot", UiModuleCode = "NEW-BOT", CurrentPrice = 399.99m });
            db.UserBots.AddRange(
                new UserBot { Id = 1, UserId = _userId, BotId = 1, PurchasePrice = 49.99m, PurchaseDate = _legacyDate },
                new UserBot { Id = 2, UserId = _userId, BotId = 2, PurchasePrice = 0m, PurchaseDate = _legacyDate.AddDays(1) },
                new UserBot { Id = 3, UserId = _otherUserId, BotId = 1, PurchasePrice = 99m, PurchaseDate = _legacyDate.AddDays(1) });
            db.Transactions.Add(new Transaction { Id = 1, UserId = _userId, Amount = 49.99m,
                Currency = "USD", PaymentMethod = "Historical", TransactionDate = _legacyDate });
            db.BotActivityLogs.Add(new BotActivityLog { Id = 1, UserId = _userId, BotId = 1,
                CorrelationId = "existing-correlation", LogLevel = "INFO", Message = "Historical log", StackTrace = "trace-secret" });
            db.ConnectedAccounts.Add(new ConnectedAccount { Id = 1, UserId = _userId, BotId = 1,
                DisplayName = "Existing account", Platform = "Facebook" });
            db.UserSettings.Add(new UserSetting { UserId = _userId, AIApiKeyEncrypted = "encrypted-secret" });
            db.UserRefreshTokens.Add(new UserRefreshToken { UserId = _userId, Token = "refresh-token-secret",
                ExpDate = DateTime.UtcNow.AddDays(1) });
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
    public async Task AdministrativeRoles_CanAssignInspectListAndRemove(string role)
    {
        Assert.False(await Owns(_userId, 3));
        var before = DateTime.UtcNow.AddSeconds(-1);
        using var created = await Send(HttpMethod.Post, Route, new { userId = _userId, botId = 3 }, role);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var assignment = await ReadSubscription(created);
        Assert.EndsWith($"{Route}/{assignment.Id}", created.Headers.Location!.ToString());
        Assert.Equal(_userId, assignment.UserId); Assert.Equal(3, assignment.BotId);
        Assert.Equal("Owner", assignment.UserFullName); Assert.Equal("owner@example.test", assignment.UserEmail);
        Assert.Equal("New bot", assignment.BotName); Assert.Equal("NEW-BOT", assignment.UiModuleCode);
        Assert.Equal(0m, assignment.PurchasePrice); Assert.True(assignment.IsActive);
        Assert.InRange(assignment.PurchaseDate, before, DateTime.UtcNow.AddSeconds(1));
        Assert.True(await Owns(_userId, 3));
        Assert.Equal(assignment, await ReadSubscription(await Send(HttpMethod.Get, $"{Route}/{assignment.Id}", role: role)));
        var list = await ReadPage(await Send(HttpMethod.Get, $"{Route}?userId={_userId}&botId=3", role: role));
        Assert.Equal(assignment, Assert.Single(list.Items));
        using var removed = await Send(HttpMethod.Delete, $"{Route}/{assignment.Id}", role: role);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.True((await removed.Content.ReadFromJsonAsync<ApiResponse<bool>>())!.Data);
        Assert.False(await Owns(_userId, 3));
        using var missing = await Send(HttpMethod.Get, $"{Route}/{assignment.Id}", role: role);
        await AssertError(missing, HttpStatusCode.NotFound);
        await AssertNoSideEffects();
    }

    [Fact]
    public async Task DuplicateAssignment_IsConflictAndPreservesLegacyPurchaseMetadata()
    {
        using var duplicate = await Send(HttpMethod.Post, Route, new { userId = _userId, botId = 1 });
        var error = await AssertError(duplicate, HttpStatusCode.Conflict);
        Assert.Equal("User is already assigned to this bot.", error.Message);
        var legacy = await ReadSubscription(await Send(HttpMethod.Get, Route + "/1"));
        Assert.Equal(49.99m, legacy.PurchasePrice); Assert.Equal(_legacyDate, legacy.PurchaseDate);
        await WithDb(async db => Assert.Equal(3, await db.UserBots.CountAsync()));
        await AssertNoSideEffects();
    }

    [Fact]
    public async Task List_FiltersByUserBotAndPresenceStateAndPaginatesDeterministically()
    {
        var all = await ReadPage(await Send(HttpMethod.Get, Route));
        Assert.Equal(new[] { 3, 2, 1 }, all.Items.Select(assignment => assignment.Id));
        Assert.Equal(3, all.TotalItems);
        var user = await ReadPage(await Send(HttpMethod.Get, $"{Route}?userId={_userId}"));
        Assert.Equal(new[] { 2, 1 }, user.Items.Select(assignment => assignment.Id));
        var bot = await ReadPage(await Send(HttpMethod.Get, Route + "?botId=1"));
        Assert.Equal(new[] { 3, 1 }, bot.Items.Select(assignment => assignment.Id));
        var combined = await ReadPage(await Send(HttpMethod.Get, $"{Route}?userId={_userId}&botId=1&isActive=true"));
        Assert.Equal(1, Assert.Single(combined.Items).Id);
        Assert.All(all.Items, assignment => Assert.True(assignment.IsActive));
        var inactive = await ReadPage(await Send(HttpMethod.Get, Route + "?isActive=false"));
        Assert.Empty(inactive.Items); Assert.Equal(0, inactive.TotalItems); Assert.Equal(0, inactive.TotalPages);
        var page = await ReadPage(await Send(HttpMethod.Get, Route + "?pageNumber=2&pageSize=2"));
        Assert.Equal(1, Assert.Single(page.Items).Id); Assert.Equal(3, page.TotalItems);
        Assert.Equal(2, page.TotalPages); Assert.Equal(2, page.CurrentPage);
        var beyond = await ReadPage(await Send(HttpMethod.Get, Route + "?pageNumber=3&pageSize=2"));
        Assert.Empty(beyond.Items); Assert.Equal(3, beyond.TotalItems);
        Assert.Empty((await ReadPage(await Send(HttpMethod.Get, $"{Route}?userId={Guid.NewGuid()}"))).Items);
        Assert.Empty((await ReadPage(await Send(HttpMethod.Get, Route + "?botId=999"))).Items);
    }

    [Fact]
    public async Task Remove_RevokesExistingOwnershipAndClientViewsWithoutDeletingResources()
    {
        using var before = await Send(HttpMethod.Get, "/api/v1/bots/1?requireOwnership=true", role: "User");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var remove = await Send(HttpMethod.Delete, Route + "/1");
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        Assert.False(await Owns(_userId, 1));
        Assert.True(await Owns(_otherUserId, 1)); Assert.True(await Owns(_userId, 2));
        using var ownedBot = await Send(HttpMethod.Get, "/api/v1/bots/1?requireOwnership=true", role: "User");
        Assert.Equal(HttpStatusCode.NotFound, ownedBot.StatusCode);
        using var subscribed = await Send(HttpMethod.Get, "/api/v1/bots/subscribed", role: "User");
        var bots = (await subscribed.Content.ReadFromJsonAsync<ApiResponse<List<BotDto>>>())!.Data!;
        Assert.DoesNotContain(bots, bot => bot.Id == 1); Assert.Contains(bots, bot => bot.Id == 2);
        using var logs = await Send(HttpMethod.Get, "/api/v1/activity-logs?botId=1", role: "User");
        Assert.Equal(HttpStatusCode.NotFound, logs.StatusCode);
        await AssertNoSideEffects();
        await WithDb(async db => Assert.Equal(2, await db.UserBots.CountAsync()));
    }

    [Fact]
    public async Task RemovedAssignment_CanBeReassignedAsNewRelationship()
    {
        using var removed = await Send(HttpMethod.Delete, Route + "/1");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var assigned = await ReadSubscription(await Send(HttpMethod.Post, Route, new { userId = _userId, botId = 1 }));
        Assert.NotEqual(1, assigned.Id); Assert.Equal(0m, assigned.PurchasePrice);
        Assert.True(await Owns(_userId, 1));
        await WithDb(async db => Assert.Equal(1, await db.UserBots.CountAsync(item => item.UserId == _userId && item.BotId == 1)));
        await AssertNoSideEffects();
    }

    [Fact]
    public async Task AssignmentPresence_IsIndependentOfUserOrBotLifecycle()
    {
        var inactiveBot = await ReadSubscription(await Send(HttpMethod.Post, Route, new { userId = _otherUserId, botId = 2 }));
        Assert.True(inactiveBot.IsActive);
        var deletedUser = await ReadSubscription(await Send(HttpMethod.Post, Route, new { userId = _deletedUserId, botId = 3 }));
        Assert.True(deletedUser.IsActive);
        using var duplicate = await Send(HttpMethod.Post, Route, new { userId = _userId, botId = 2 });
        await AssertError(duplicate, HttpStatusCode.Conflict);
        await WithDb(async db =>
        {
            Assert.True((await db.Users.FindAsync(_deletedUserId))!.IsDeleted);
            Assert.False((await db.Bots.FindAsync(2))!.IsActive);
        });
    }

    public static IEnumerable<object[]> BlockedRequests()
    {
        foreach (var operation in new[] { "list", "detail", "assign", "remove" })
        foreach (var role in new[] { "Anonymous", "User", "Support", "Worker", "SystemAdmin" })
            yield return [operation, role];
    }

    [Theory]
    [MemberData(nameof(BlockedRequests))]
    public async Task EveryEndpoint_RejectsAnonymousAndNonAdministrativeRoles(string operation, string role)
    {
        var (method, route, body) = RequestFor(operation, 1);
        using var response = await Send(method, route, body, role == "Anonymous" ? null : role);
        Assert.Equal(role == "Anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
        await WithDb(async db => Assert.Equal(3, await db.UserBots.CountAsync()));
    }

    [Theory]
    [InlineData("inactive", 401)]
    [InlineData("missing", 401)]
    [InlineData("empty-id", 401)]
    [InlineData("changed-role", 403)]
    public async Task StaleAdminIdentityOrRole_CannotAssign(string state, int expectedStatus)
    {
        var actor = state == "missing" ? Guid.NewGuid() : state == "empty-id" ? Guid.Empty : _adminId;
        await WithDb(async db =>
        {
            var admin = await db.SystmeAdmins.FindAsync(_adminId);
            if (state == "inactive") admin!.IsActive = false;
            if (state == "changed-role") admin!.Role = "Support";
            await db.SaveChangesAsync();
        });
        using var response = await Send(HttpMethod.Post, Route, new { userId = _userId, botId = 3 }, actorId: actor);
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        await WithDb(async db => Assert.Equal(3, await db.UserBots.CountAsync()));
    }

    [Theory]
    [InlineData("user")]
    [InlineData("bot")]
    public async Task Assign_ValidatesBothResourceIdsBeforeWriting(string missing)
    {
        using var response = await Send(HttpMethod.Post, Route,
            new { userId = missing == "user" ? Guid.NewGuid() : _userId, botId = missing == "bot" ? 999 : 3 });
        var error = await AssertError(response, HttpStatusCode.NotFound);
        Assert.Equal(missing == "user" ? "User not found." : "Bot not found.",
            error.Message);
        await WithDb(async db => Assert.Equal(3, await db.UserBots.CountAsync()));
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("remove")]
    public async Task MissingAssignment_ReturnsStandardNotFound(string operation)
    {
        var (method, route, body) = RequestFor(operation, 999);
        using var response = await Send(method, route, body);
        await AssertError(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RepeatedRemoval_ReturnsNotFound()
    {
        using var first = await Send(HttpMethod.Delete, Route + "/1");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var second = await Send(HttpMethod.Delete, Route + "/1");
        await AssertError(second, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"userId\":null,\"botId\":3}")]
    [InlineData("{\"userId\":\"invalid\",\"botId\":3}")]
    [InlineData("{\"userId\":\"00000000-0000-0000-0000-000000000000\",\"botId\":3}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task InvalidAssignBody_IsRejected(string json)
    {
        using var response = await Send(HttpMethod.Post, Route, rawJson: json);
        await AssertError(response, HttpStatusCode.BadRequest);
        await WithDb(async db => Assert.Equal(3, await db.UserBots.CountAsync()));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("\"invalid\"")]
    public async Task InvalidBotId_IsRejected(string value)
    {
        using var response = await Send(HttpMethod.Post, Route, rawJson: $"{{\"userId\":\"{_userId}\",\"botId\":{value}}}");
        await AssertError(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("\"purchasePrice\":9.99")]
    [InlineData("\"purchaseDate\":\"2020-01-01T00:00:00Z\"")]
    [InlineData("\"isActive\":false")]
    [InlineData("\"id\":99")]
    [InlineData("\"user\":{}")]
    public async Task ServerManagedOrPaymentFields_AreRejected(string property)
    {
        using var response = await Send(HttpMethod.Post, Route,
            rawJson: $"{{\"userId\":\"{_userId}\",\"botId\":3,{property}}}");
        await AssertError(response, HttpStatusCode.BadRequest);
        await WithDb(async db => Assert.Equal(3, await db.UserBots.CountAsync()));
        await AssertNoSideEffects();
    }

    [Theory]
    [InlineData("?userId=invalid")]
    [InlineData("?userId=00000000-0000-0000-0000-000000000000")]
    [InlineData("?botId=0")]
    [InlineData("?isActive=invalid")]
    [InlineData("?pageNumber=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageNumber=2147483647&pageSize=100")]
    public async Task InvalidFilters_ReturnStandardBadRequest(string query)
    {
        using var response = await Send(HttpMethod.Get, Route + query);
        await AssertError(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ZeroAssignmentIds_AreValidated()
    {
        using var detail = await Send(HttpMethod.Get, Route + "/0");
        await AssertError(detail, HttpStatusCode.BadRequest);
        using var remove = await Send(HttpMethod.Delete, Route + "/0");
        await AssertError(remove, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Detail_OnlyReturnsAllowlistedRelationshipAndDisplayFields()
    {
        using var response = await Send(HttpMethod.Get, Route + "/1");
        var json = await response.Content.ReadAsStringAsync();
        foreach (var secret in new[] { Session, "password-hash-secret", "internal-ip-secret", "encrypted-secret", "refresh-token-secret", "trace-secret" })
            Assert.DoesNotContain(secret, json);
        var dto = await ReadSubscription(response);
        Assert.Equal(49.99m, dto.PurchasePrice); Assert.Equal(_legacyDate, dto.PurchaseDate);
    }

    [Fact]
    public async Task DatabaseUniquePairIndex_RejectsDuplicatesWithoutApiPrecheck()
    {
        await WithDb(async db =>
        {
            db.UserBots.Add(new UserBot { UserId = _userId, BotId = 1 });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(19, Assert.IsType<SqliteException>(exception.InnerException).SqliteErrorCode);
        });
        await WithDb(async db => Assert.Equal(3, await db.UserBots.CountAsync()));
    }

    [Fact]
    public void SqlServerMigration_AddsUniquePairWithoutRewritingPurchaseMetadata()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var entity = db.Model.FindEntityType(typeof(UserBot))!;
        var index = Assert.Single(entity.GetIndexes(), item => item.GetDatabaseName() == "IX_UserBots_UserId_BotId");
        Assert.True(index.IsUnique);
        var migration = db.GetService<IMigrationsAssembly>().Migrations.Keys.Single(key => key.EndsWith("AddUniqueUserBotAssignment"));
        var sql = db.GetService<IMigrator>().GenerateScript("20261014163726_AddUniqueBotUiModuleCode", migration);
        Assert.Contains("CREATE UNIQUE INDEX [IX_UserBots_UserId_BotId] ON [UserBots] ([UserId], [BotId])", sql);
        Assert.DoesNotContain("UPDATE [UserBots]", sql); Assert.DoesNotContain("DELETE FROM [UserBots]", sql);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private async Task AssertNoSideEffects() => await WithDb(async db =>
    {
        Assert.Equal(3, await db.Users.CountAsync()); Assert.Equal(3, await db.Bots.CountAsync());
        Assert.Equal(1, await db.ConnectedAccounts.CountAsync()); Assert.Equal(1, await db.BotActivityLogs.CountAsync());
        Assert.Equal("encrypted-secret", (await db.UserSettings.SingleAsync()).AIApiKeyEncrypted);
        Assert.True((await db.UserRefreshTokens.SingleAsync()).IsActive);
        var transaction = Assert.Single(await db.Transactions.ToListAsync());
        Assert.Equal(49.99m, transaction.Amount); Assert.Equal("Historical", transaction.PaymentMethod);
        Assert.Equal("USD", transaction.Currency); Assert.Equal(_legacyDate, transaction.TransactionDate);
    });

    private async Task<bool> Owns(Guid userId, int botId)
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUserBotService>().CheckOwnershipAsync(userId, botId, CancellationToken.None);
    }

    private (HttpMethod Method, string Route, object? Body) RequestFor(string operation, int id) => operation switch
    {
        "list" => (HttpMethod.Get, Route, null),
        "detail" => (HttpMethod.Get, $"{Route}/{id}", null),
        "assign" => (HttpMethod.Post, Route, new { userId = _userId, botId = 3 }),
        _ => (HttpMethod.Delete, $"{Route}/{id}", null)
    };

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

    private static async Task<AdminSubscriptionDto> ReadSubscription(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            AssertPublicFields(json.RootElement.GetProperty("data"));
            return (await response.Content.ReadFromJsonAsync<ApiResponse<AdminSubscriptionDto>>())!.Data!;
        }
    }

    private static async Task<PaginatedResult<AdminSubscriptionDto>> ReadPage(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var assignment in json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
                AssertPublicFields(assignment);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<PaginatedResult<AdminSubscriptionDto>>>())!.Data!;
        }
    }

    private static void AssertPublicFields(JsonElement assignment) => Assert.Equal(
        new[] { "botId", "botName", "id", "isActive", "purchaseDate", "purchasePrice", "uiModuleCode", "userEmail", "userFullName", "userId" },
        assignment.EnumerateObject().Select(property => property.Name).Order());

    private static async Task<ApiErrorResponse> AssertError(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.False(error!.IsSuccess); Assert.Equal((int)status, error.StatusCode);
        Assert.False(string.IsNullOrEmpty(error.Path)); Assert.False(string.IsNullOrEmpty(error.TraceId));
        Assert.Null(error.Details);
        return error;
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = _host.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
