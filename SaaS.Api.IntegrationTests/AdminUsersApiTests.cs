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

// Real HTTP routing, cookie JWTs, admin lookups, BCrypt, auth/session handlers and relational EF.
public sealed class AdminUsersApiTests : IAsyncLifetime
{
    private const string Route = "/api/v1/admin/users";
    private const string Password = "integration-password-123!";
    private const string Session = "user-session-secret";
    private const string Refresh = "refresh-token-secret";
    private readonly Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _secondUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _deletedUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _superAdminId = Guid.NewGuid();
    private readonly SecuritySettings _security = new()
    {
        JwtSecret = "admin-users-integration-signing-key-at-least-32-bytes",
        JwtIssuer = "admin-users-tests", JwtAudience = "admin-users-tests", PasswordWorkFactor = 11
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
                    services.AddControllers().AddApplicationPart(typeof(AdminUsersController).Assembly);
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
                    });
                    services.AddScoped<IPasswordHasherService, PasswordHasherService>();
                    services.AddScoped<ITokenService, TokenService>();
                    services.AddScoped<IAuthSessionService, AuthSessionService>();
                    services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
                })
                .Configure(app =>
                {
                    app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
                    app.UseMiddleware<ExceptionMiddleware>();
                    app.UseMiddleware<SingleActiveSessionMiddleware>();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                })).StartAsync();
        _client = _host.GetTestClient();
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>().HashPassword(Password);
        var time = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        db.Users.AddRange(
            new User { Id = _userId, FullName = "Alice Owner", Email = "alice@example.test", PhoneNumber = "123",
                PasswordHash = hash, CurrentSessionToken = Session, LastLoginIp = "internal-ip-secret", CreatedAt = time },
            new User { Id = _secondUserId, FullName = "Bob Other", Email = "bob@example.test", PasswordHash = hash, CreatedAt = time },
            new User { Id = _deletedUserId, FullName = "Inactive Owner", Email = "inactive@example.test",
                PasswordHash = hash, IsDeleted = true, CurrentSessionToken = Session, CreatedAt = time.AddMinutes(-1) });
        db.SystmeAdmins.AddRange(
            new SystemAdmin { Id = _adminId, FullName = "Admin", Email = "admin@example.test", PasswordHash = hash, Role = "Admin" },
            new SystemAdmin { Id = _superAdminId, FullName = "Super admin", Email = "super@example.test", PasswordHash = hash, Role = "SuperAdmin" });
        db.UserSettings.Add(new UserSetting { UserId = _userId, AIApiKeyEncrypted = "encrypted-secret",
            ScraperApiTokenEncrypted = "scraper-secret" });
        db.UserRefreshTokens.AddRange(new UserRefreshToken { UserId = _userId, Token = Refresh, ExpDate = DateTime.UtcNow.AddDays(1) },
            new UserRefreshToken { UserId = _deletedUserId, Token = "deleted-refresh-secret", ExpDate = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose(); await _host.StopAsync(); _host.Dispose(); await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task AdministrativeRoles_CanListCreateReadUpdateDeactivateAndReactivate(string role)
    {
        var list = await ReadPage(await Send(HttpMethod.Get, Route, role: role));
        Assert.Equal(3, list.TotalItems);
        using var created = await Send(HttpMethod.Post, Route, new
            { fullName = " New User ", email = " new@example.test ", password = Password, phoneNumber = " 456 " }, role);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = await ReadUser(created);
        Assert.Equal("New User", user.FullName); Assert.Equal("new@example.test", user.Email);
        Assert.Equal("456", user.PhoneNumber); Assert.False(user.IsDeleted);
        Assert.EndsWith($"{Route}/{user.Id}", created.Headers.Location!.ToString());
        Assert.False(created.Headers.Contains("Set-Cookie"));
        var detail = await ReadUser(await Send(HttpMethod.Get, $"{Route}/{user.Id}", role: role));
        Assert.Equal(user, detail);
        var updated = await ReadUser(await Send(HttpMethod.Put, $"{Route}/{user.Id}", new
            { fullName = "Edited User", email = "edited@example.test", phoneNumber = "789" }, role));
        Assert.Equal("Edited User", updated.FullName); Assert.Equal(user.CreatedAt, updated.CreatedAt);
        var inactive = await ReadUser(await Send(HttpMethod.Post, $"{Route}/{user.Id}/deactivate", role: role));
        Assert.True(inactive.IsDeleted);
        var active = await ReadUser(await Send(HttpMethod.Post, $"{Route}/{user.Id}/reactivate", role: role));
        Assert.False(active.IsDeleted);
        await WithDb(async db =>
        {
            var entity = await db.Users.SingleAsync(candidate => candidate.Id == user.Id);
            using var scope = _host.Services.CreateScope();
            Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasherService>().VerifyPassword(Password, entity.PasswordHash));
            Assert.Null(entity.CurrentSessionToken);
            Assert.Empty(await db.UserRefreshTokens.Where(token => token.UserId == user.Id).ToListAsync());
            Assert.Equal(4, await db.Users.CountAsync());
            Assert.Equal(2, await db.SystmeAdmins.CountAsync());
        });
    }

    public static IEnumerable<object[]> BlockedRequests()
    {
        foreach (var operation in new[] { "list", "detail", "create", "update", "deactivate", "reactivate" })
        foreach (var role in new[] { "Anonymous", "User", "Support", "Worker", "SystemAdmin" })
            yield return [operation, role];
    }

    [Theory]
    [MemberData(nameof(BlockedRequests))]
    public async Task AllEndpoints_RejectAnonymousAndNonAdministrativeRoles(string operation, string role)
    {
        var (method, route, body) = RequestFor(operation);
        using var response = await Send(method, route, body, role == "Anonymous" ? null : role,
            actorId: role == "User" ? _userId : _adminId);
        Assert.Equal(role == "Anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
        await WithDb(async db =>
        {
            Assert.Equal(3, await db.Users.CountAsync());
            var owner = await db.Users.FindAsync(_userId);
            Assert.Equal("Alice Owner", owner!.FullName); Assert.False(owner.IsDeleted);
            Assert.Equal(Session, owner.CurrentSessionToken);
        });
    }

    [Theory]
    [InlineData("inactive", 401)]
    [InlineData("missing", 401)]
    [InlineData("empty-id", 401)]
    [InlineData("changed-role", 403)]
    public async Task StaleAdminTokens_AreRejectedWithoutCreatingUsers(string state, int status)
    {
        var actor = _adminId;
        if (state == "missing") actor = Guid.NewGuid();
        if (state == "empty-id") actor = Guid.Empty;
        await WithDb(async db =>
        {
            var admin = await db.SystmeAdmins.FindAsync(_adminId);
            if (state == "inactive") admin!.IsActive = false;
            if (state == "changed-role") admin!.Role = "Support";
            await db.SaveChangesAsync();
        });
        using var response = await Send(HttpMethod.Post, Route,
            new { fullName = "Denied", email = "denied@example.test", password = Password }, actorId: actor);
        Assert.Equal(status, (int)response.StatusCode);
        await WithDb(async db => Assert.Equal(3, await db.Users.CountAsync()));
    }

    [Fact]
    public async Task PaginationAndFilters_IncludeDeletedUsersAndUseDeterministicOrdering()
    {
        var first = await ReadPage(await Send(HttpMethod.Get, Route + "?pageSize=1"));
        var second = await ReadPage(await Send(HttpMethod.Get, Route + "?pageSize=1&pageNumber=2"));
        Assert.Equal(_userId, Assert.Single(first.Items).Id);
        Assert.Equal(_secondUserId, Assert.Single(second.Items).Id);
        Assert.Equal(3, first.TotalItems); Assert.Equal(3, first.TotalPages); Assert.Equal(2, second.CurrentPage);
        var deleted = await ReadPage(await Send(HttpMethod.Get, Route + "?isDeleted=true"));
        Assert.Equal(_deletedUserId, Assert.Single(deleted.Items).Id);
        var active = await ReadPage(await Send(HttpMethod.Get, Route + "?isDeleted=false"));
        Assert.Equal(2, active.TotalItems); Assert.All(active.Items, user => Assert.False(user.IsDeleted));
        var nameSearch = await ReadPage(await Send(HttpMethod.Get, Route + "?search=ALICE"));
        Assert.Equal(_userId, Assert.Single(nameSearch.Items).Id);
        var emailSearch = await ReadPage(await Send(HttpMethod.Get, Route + "?search=%20BOB%40example%20"));
        Assert.Equal(_secondUserId, Assert.Single(emailSearch.Items).Id);
        var combined = await ReadPage(await Send(HttpMethod.Get, Route + "?search=Owner&isDeleted=true&pageSize=1"));
        Assert.Equal(_deletedUserId, Assert.Single(combined.Items).Id);
        var empty = await ReadPage(await Send(HttpMethod.Get, Route + "?search=nonexistent"));
        Assert.Empty(empty.Items); Assert.Equal(0, empty.TotalPages);
        var beyond = await ReadPage(await Send(HttpMethod.Get, Route + "?pageNumber=4&pageSize=1"));
        Assert.Empty(beyond.Items); Assert.Equal(3, beyond.TotalItems); Assert.Equal(4, beyond.CurrentPage);
    }

    [Fact]
    public async Task Detail_CanReadDeactivatedUserAndReturnsOnlyPublicFields()
    {
        var deleted = await ReadUser(await Send(HttpMethod.Get, $"{Route}/{_deletedUserId}"));
        Assert.True(deleted.IsDeleted);
        using var response = await Send(HttpMethod.Get, $"{Route}/{_userId}");
        var json = await response.Content.ReadAsStringAsync();
        foreach (var secret in new[] { Password, Session, Refresh, "internal-ip-secret", "encrypted-secret", "scraper-secret" })
            Assert.DoesNotContain(secret, json);
        await ReadUser(response);
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("update")]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    public async Task MissingUsers_ReturnStandardNotFound(string operation)
    {
        var (method, _, body) = RequestFor(operation);
        var suffix = operation is "deactivate" or "reactivate" ? "/" + operation : "";
        using var response = await Send(method, $"{Route}/{Guid.NewGuid()}{suffix}", body);
        await AssertError(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("ALICE@example.test")]
    [InlineData(" inactive@example.test ")]
    public async Task Create_DuplicateIncludingDeactivatedEmail_IsConflict(string email)
    {
        using var response = await Send(HttpMethod.Post, Route, new { fullName = "Duplicate", email, password = Password });
        await AssertError(response, HttpStatusCode.Conflict);
        await WithDb(async db => Assert.Equal(3, await db.Users.CountAsync()));
    }

    [Fact]
    public async Task Update_DuplicateEmailDoesNotChangeProfileAndOwnEmailIsAllowed()
    {
        using var conflict = await Send(HttpMethod.Put, $"{Route}/{_userId}",
            new { fullName = "Changed", email = "BOB@example.test", phoneNumber = "999" });
        await AssertError(conflict, HttpStatusCode.Conflict);
        await WithDb(async db => Assert.Equal("Alice Owner", (await db.Users.FindAsync(_userId))!.FullName));
        var result = await ReadUser(await Send(HttpMethod.Put, $"{Route}/{_userId}",
            new { fullName = "Updated", email = "alice@example.test" }));
        Assert.Equal("", result.PhoneNumber);
        await WithDb(async db =>
        {
            var user = await db.Users.FindAsync(_userId);
            Assert.Equal(Session, user!.CurrentSessionToken); Assert.False(user.IsDeleted);
            Assert.True((await db.UserRefreshTokens.SingleAsync(token => token.UserId == _userId)).IsActive);
            Assert.NotNull(await db.UserSettings.SingleAsync(setting => setting.UserId == _userId));
        });
    }

    [Fact]
    public async Task Deactivate_BlocksLoginRefreshAndExistingSession_ReactivateRequiresFreshLogin()
    {
        var inactive = await ReadUser(await Send(HttpMethod.Post, $"{Route}/{_userId}/deactivate"));
        Assert.True(inactive.IsDeleted);
        await WithDb(async db =>
        {
            var user = await db.Users.FindAsync(_userId);
            Assert.NotNull(user); Assert.Null(user.CurrentSessionToken);
            var token = await db.UserRefreshTokens.SingleAsync(token => token.UserId == _userId);
            Assert.False(token.IsActive); Assert.True(token.ExpDate <= DateTime.UtcNow);
            Assert.NotNull(await db.UserSettings.SingleAsync(setting => setting.UserId == _userId));
        });
        using var session = await Send(HttpMethod.Get, "/api/v1/auth/me", role: "User", actorId: _userId);
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        using var login = await Login("alice@example.test");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        using var refresh = await RefreshSession(Refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // Repeating either operation is safe. Old cookies remain invalid after reactivation.
        Assert.True((await ReadUser(await Send(HttpMethod.Post, $"{Route}/{_userId}/deactivate"))).IsDeleted);
        Assert.False((await ReadUser(await Send(HttpMethod.Post, $"{Route}/{_userId}/reactivate"))).IsDeleted);
        using var oldSession = await Send(HttpMethod.Get, "/api/v1/auth/me", role: "User", actorId: _userId);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        using var oldRefresh = await RefreshSession(Refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        using var fresh = await Login("alice@example.test");
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        me.Headers.Add("Cookie", Cookies(fresh));
        using var authenticated = await _client.SendAsync(me);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        Assert.False((await ReadUser(await Send(HttpMethod.Post, $"{Route}/{_userId}/reactivate"))).IsDeleted);
        await WithDb(async db => Assert.NotNull((await db.Users.FindAsync(_userId))!.CurrentSessionToken));
    }

    [Fact]
    public async Task AlreadyDeletedUser_WithLeftoverSessionAndActiveRefresh_IsRejected()
    {
        using var session = await Send(HttpMethod.Get, "/api/v1/activity-logs", role: "User", actorId: _deletedUserId);
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        using var refresh = await RefreshSession("deleted-refresh-secret");
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using var login = await Login("inactive@example.test");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task CreatedUser_CanLoginWithProjectPasswordHasherAndAdminLoginCookieCanManageUsers()
    {
        var user = await ReadUser(await Send(HttpMethod.Post, Route,
            new { fullName = "Created", email = "created@example.test", password = Password }));
        using var login = await Login(user.Email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var adminLogin = await Send(HttpMethod.Post, "/api/v1/admin/auth/login",
            new { email = "admin@example.test", password = Password }, role: null);
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add("Cookie", Cookies(adminLogin));
        await ReadPage(await _client.SendAsync(request));
    }

    [Theory]
    [InlineData("?pageNumber=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageNumber=2147483647&pageSize=100")]
    [InlineData("?pageSize=oops")]
    [InlineData("?isDeleted=invalid")]
    public async Task InvalidListFilters_ReturnStandardBadRequest(string query)
    {
        using var response = await Send(HttpMethod.Get, Route + query);
        await AssertError(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"fullName\":\" \",\"email\":\"ok@example.test\",\"password\":\"test\"}")]
    [InlineData("{\"fullName\":\"Test\",\"email\":\"invalid\",\"password\":\"test\"}")]
    [InlineData("{\"fullName\":\"Test\",\"email\":\"ok@example.test\",\"password\":\" \"}")]
    [InlineData("{\"fullName\":\"Test\",\"email\":\"ok@example.test\",\"password\":null}")]
    [InlineData("{\"fullName\":\"Test\",\"email\":\"ok@example.test\",\"password\":\"test\",\"isDeleted\":true}")]
    [InlineData("{\"fullName\":\"Test\",\"email\":\"ok@example.test\",\"password\":\"test\",\"role\":\"SuperAdmin\"}")]
    [InlineData("{\"fullName\":\"Test\",\"email\":\"ok@example.test\",\"passwordHash\":\"hash\"}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task InvalidCreateBodies_AreRejectedWithoutWrites(string json)
    {
        using var response = await Send(HttpMethod.Post, Route, rawJson: json);
        await AssertError(response, HttpStatusCode.BadRequest);
        await WithDb(async db => Assert.Equal(3, await db.Users.CountAsync()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"fullName\":\"Test\",\"email\":\"ok@example.test\",\"isDeleted\":true}")]
    [InlineData("{\"fullName\":\"Test\",\"email\":\"ok@example.test\",\"password\":\"new-password\"}")]
    [InlineData("{\"fullName\":\"Test\",\"email\":\"ok@example.test\",\"currentSessionToken\":\"forged\"}")]
    public async Task InvalidUpdateBodies_CannotChangeStatusOrSecurityFields(string json)
    {
        using var response = await Send(HttpMethod.Put, $"{Route}/{_userId}", rawJson: json);
        await AssertError(response, HttpStatusCode.BadRequest);
        await WithDb(async db =>
        {
            var user = await db.Users.FindAsync(_userId);
            Assert.Equal("Alice Owner", user!.FullName); Assert.False(user.IsDeleted);
            Assert.Equal(Session, user.CurrentSessionToken);
        });
    }

    [Fact]
    public async Task ProfileLengthAndEmptyIds_AreValidated()
    {
        using var name = await Send(HttpMethod.Post, Route,
            new { fullName = new string('x', 101), email = "valid@example.test", password = Password });
        await AssertError(name, HttpStatusCode.BadRequest);
        using var email = await Send(HttpMethod.Post, Route,
            new { fullName = "Test", email = new string('x', 245) + "@example.test", password = Password });
        await AssertError(email, HttpStatusCode.BadRequest);
        using var emptyId = await Send(HttpMethod.Get, $"{Route}/{Guid.Empty}");
        await AssertError(emptyId, HttpStatusCode.BadRequest);
        using var search = await Send(HttpMethod.Get, Route + "?search=" + new string('x', 256));
        await AssertError(search, HttpStatusCode.BadRequest);
    }

    private (HttpMethod Method, string Route, object? Body) RequestFor(string operation) => operation switch
    {
        "list" => (HttpMethod.Get, Route, null),
        "detail" => (HttpMethod.Get, $"{Route}/{_userId}", null),
        "create" => (HttpMethod.Post, Route, new { fullName = "New", email = "new@example.test", password = Password }),
        "update" => (HttpMethod.Put, $"{Route}/{_userId}", new { fullName = "Updated", email = "alice@example.test" }),
        _ => (HttpMethod.Post, $"{Route}/{_userId}/{operation}", null)
    };

    private Task<HttpResponseMessage> Login(string email) => Send(HttpMethod.Post, "/api/v1/auth/login",
        new { email, password = Password }, role: null);

    private async Task<HttpResponseMessage> RefreshSession(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{TokenService.RefreshTokenCookieName}={token}");
        return await _client.SendAsync(request);
    }

    private static string Cookies(HttpResponseMessage response) =>
        string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]));

    private async Task<HttpResponseMessage> Send(HttpMethod method, string route, object? body = null,
        string? role = "Admin", Guid? actorId = null, string? rawJson = null)
    {
        using var request = new HttpRequestMessage(method, route);
        if (role is not null)
        {
            var id = actorId ?? (role == "SuperAdmin" ? _superAdminId : _adminId);
            var token = new JwtSecurityToken(_security.JwtIssuer, _security.JwtAudience,
                [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role),
                    new Claim("SessionToken", Session)], expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_security.JwtSecret)),
                    SecurityAlgorithms.HmacSha256));
            request.Headers.Add("Cookie", $"{TokenService.AccessTokenCookieName}={new JwtSecurityTokenHandler().WriteToken(token)}");
        }
        if (rawJson is not null) request.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        else if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static async Task<AdminUserDto> ReadUser(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            AssertPublicFields(json.RootElement.GetProperty("data"));
            return (await response.Content.ReadFromJsonAsync<ApiResponse<AdminUserDto>>())!.Data!;
        }
    }

    private static async Task<PaginatedResult<AdminUserDto>> ReadPage(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var user in json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
                AssertPublicFields(user);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<PaginatedResult<AdminUserDto>>>())!.Data!;
        }
    }

    private static void AssertPublicFields(JsonElement user) => Assert.Equal(
        new[] { "createdAt", "email", "fullName", "id", "isDeleted", "phoneNumber" },
        user.EnumerateObject().Select(property => property.Name).Order());

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
