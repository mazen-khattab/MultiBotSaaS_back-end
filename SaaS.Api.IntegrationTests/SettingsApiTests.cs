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
using SaaS.Infrastructure.Extensions;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Services;

namespace SaaS.Api.IntegrationTests;

// Exercises HTTP routing, cookie JWT authentication, role/session authorization,
// JSON binding, the real MediatR validation pipeline, handlers and AES encryption.
// SQLite uses the production EF model with SQL Server type/default syntax adapted.
public sealed class SettingsApiTests : IAsyncLifetime
{
    private const string Route = "/api/v1/settings";
    private const string Session = "settings-test-session";
    private const string AiKey = "test-ai-secret-1234";
    private const string ScraperToken = "test-scraper-secret-5678";
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly SecuritySettings _security = new()
    {
        JwtSecret = "settings-integration-signing-key-at-least-32-bytes",
        JwtIssuer = "settings-tests",
        JwtAudience = "settings-tests",
        EncryptionKey = "settings-integration-encryption-key"
    };
    private IHost _host = null!;
    private HttpClient _client = null!;
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _host = await new HostBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHost(web => web.UseTestServer().UseEnvironment("Production")
                .ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(SettingsController).Assembly);
                    services.AddJwtAuthentication(_security);
                    services.AddAuthorization();
                    services.AddMediatRWithValidation();
                    services.AddScoped<AppDbContext>(_ => new SqliteAppDbContext(
                        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options));
                    services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
                    services.Configure<SecuritySettings>(options => options.EncryptionKey = _security.EncryptionKey);
                    services.AddSingleton<IEncryptionService, EncryptionService>();
                    services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
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
            db.Users.AddRange(
                new() { Id = _userId, Email = "first@example.test", CurrentSessionToken = Session },
                new() { Id = _otherUserId, Email = "second@example.test", CurrentSessionToken = Session });
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
    public async Task Get_Authenticated_ReturnsOnlyOwnSettingsAndMaskedCredentials()
    {
        await Put(_userId, new { aiApiKey = AiKey, scraperToken = ScraperToken, dailyLeadLimit = 17 });
        await Put(_otherUserId, new { aiApiKey = "other-secret-9999", dailyLeadLimit = 91 });

        using var response = await Send(HttpMethod.Get, $"{Route}?userId={_otherUserId}", _userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<UserSettingsDto>>();
        Assert.True(envelope!.IsSuccess);
        Assert.Equal("Settings retrieved successfully.", envelope.Message);
        Assert.Equal(17, envelope.Data!.DailyLeadLimit);
        Assert.True(envelope.Data.HasAIApiKey);
        Assert.True(envelope.Data.HasScraperToken);
        Assert.EndsWith("1234", envelope.Data.MaskedAIApiKey);
        Assert.EndsWith("5678", envelope.Data.MaskedScraperToken);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(AiKey, json);
        Assert.DoesNotContain(ScraperToken, json);
        Assert.DoesNotContain("Encrypted", json, StringComparison.OrdinalIgnoreCase);
        await WithDb(async db =>
        {
            var setting = await db.UserSettings.SingleAsync(s => s.UserId == _userId);
            Assert.DoesNotContain(setting.AIApiKeyEncrypted!, json);
            Assert.DoesNotContain(setting.ScraperApiTokenEncrypted!, json);
        });
    }

    [Fact]
    public async Task Get_WithoutSettings_ReturnsExistingDefaultsWithoutCreatingRecord()
    {
        using var response = await Send(HttpMethod.Get, Route, _userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<UserSettingsDto>>();
        Assert.Equal("No settings found for the user.", envelope!.Message);
        Assert.Equal(new UserSettingsDto(false, "", false, "", 0), envelope.Data);
        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("data").GetProperty("companyName").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("data").GetProperty("companyPitch").ValueKind);
        await WithDb(async db => Assert.Empty(await db.UserSettings.ToListAsync()));
    }

    [Fact]
    public async Task Put_CreatesThenUpdatesOwnSettingsWithCorrectEncryptedFields()
    {
        using var created = await Put(_userId, new { aiApiKey = AiKey, scraperToken = ScraperToken, dailyLeadLimit = 12 });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var result = await created.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        Assert.True(result!.IsSuccess);
        Assert.Equal(_userId, result.Data);
        Assert.Equal("Settings updated successfully.", result.Message);

        using var updated = await Put(_userId, new { aiApiKey = "new-ai-secret-4321", scraperToken = "new-scraper-secret-8765", dailyLeadLimit = 23 });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await WithDb(async db =>
        {
            var setting = Assert.Single(await db.UserSettings.ToListAsync());
            var encryption = _host.Services.GetRequiredService<IEncryptionService>();
            Assert.Equal(_userId, setting.UserId);
            Assert.Equal("new-ai-secret-4321", encryption.Decrypt(setting.AIApiKeyEncrypted!));
            Assert.Equal("new-scraper-secret-8765", encryption.Decrypt(setting.ScraperApiTokenEncrypted!));
            Assert.Equal(23, setting.DailyMessageLimit);
        });
        using var get = await Send(HttpMethod.Get, Route, _userId);
        var settings = (await get.Content.ReadFromJsonAsync<ApiResponse<UserSettingsDto>>())!.Data!;
        Assert.EndsWith("4321", settings.MaskedAIApiKey);
        Assert.EndsWith("8765", settings.MaskedScraperToken);
    }

    [Fact]
    public async Task Put_ExplicitZeroOnCreation_PreservesZeroInsteadOfDatabaseDefault()
    {
        using var response = await Put(_userId, new { dailyLeadLimit = 0 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithDb(async db => Assert.Equal(0, (await db.UserSettings.SingleAsync()).DailyMessageLimit));
    }

    [Fact]
    public async Task Put_UpdatesOnlyOwnRecordAndPreservesCompanyFields()
    {
        await Put(_userId, new { dailyLeadLimit = 10 });
        await Put(_otherUserId, new { dailyLeadLimit = 90 });
        await WithDb(async db =>
        {
            var setting = await db.UserSettings.SingleAsync(s => s.UserId == _userId);
            setting.CompanyName = "Existing company";
            setting.CompanyPitch = "Existing pitch";
            await db.SaveChangesAsync();
        });

        using var response = await Put(_userId, new { dailyLeadLimit = 20 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithDb(async db =>
        {
            var own = await db.UserSettings.SingleAsync(s => s.UserId == _userId);
            var other = await db.UserSettings.SingleAsync(s => s.UserId == _otherUserId);
            Assert.Equal(20, own.DailyMessageLimit);
            Assert.Equal("Existing company", own.CompanyName);
            Assert.Equal("Existing pitch", own.CompanyPitch);
            Assert.Equal(90, other.DailyMessageLimit);
        });
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"aiApiKey\":null,\"scraperToken\":null}")]
    [InlineData("{\"aiApiKey\":\"\",\"scraperToken\":\"   \"}")]
    public async Task Put_OmittedNullOrBlankCredentials_PreservesExistingSecrets(string credentials)
    {
        await Put(_userId, new { aiApiKey = AiKey, scraperToken = ScraperToken, dailyLeadLimit = 12 });
        var body = credentials.Insert(1, "\"dailyLeadLimit\":0" + (credentials.Length > 2 ? "," : ""));
        using var response = await Send(HttpMethod.Put, Route, _userId, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithDb(async db =>
        {
            var setting = await db.UserSettings.SingleAsync();
            var encryption = _host.Services.GetRequiredService<IEncryptionService>();
            Assert.Equal(AiKey, encryption.Decrypt(setting.AIApiKeyEncrypted!));
            Assert.Equal(ScraperToken, encryption.Decrypt(setting.ScraperApiTokenEncrypted!));
            Assert.Equal(0, setting.DailyMessageLimit);
        });
    }

    [Fact]
    public async Task Put_CompanyFields_CreateUpdateAndReadOnlyAuthenticatedUserCompany()
    {
        using var created = await Put(_userId, new
        {
            dailyLeadLimit = 30, companyName = "First company", companyPitch = "First pitch"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var firstGet = await Send(HttpMethod.Get, Route, _userId);
        var first = (await firstGet.Content.ReadFromJsonAsync<ApiResponse<UserSettingsDto>>())!.Data!;
        Assert.Equal("First company", first.CompanyName);
        Assert.Equal("First pitch", first.CompanyPitch);

        using var other = await Put(_otherUserId, new
        {
            dailyLeadLimit = 70, companyName = "Other company", companyPitch = "Other pitch"
        });
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        using var updated = await Put(_userId, new
        {
            dailyLeadLimit = 40, companyName = "Updated company", companyPitch = "Updated pitch"
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var get = await Send(HttpMethod.Get, Route, _userId);
        var settings = (await get.Content.ReadFromJsonAsync<ApiResponse<UserSettingsDto>>())!.Data!;
        Assert.Equal("Updated company", settings.CompanyName);
        Assert.Equal("Updated pitch", settings.CompanyPitch);
        await WithDb(async db =>
        {
            var own = await db.UserSettings.SingleAsync(setting => setting.UserId == _userId);
            var otherSetting = await db.UserSettings.SingleAsync(setting => setting.UserId == _otherUserId);
            Assert.Equal("Updated company", own.CompanyName);
            Assert.Equal("Updated pitch", own.CompanyPitch);
            Assert.Equal("Other company", otherSetting.CompanyName);
            Assert.Equal("Other pitch", otherSetting.CompanyPitch);
        });
    }

    [Theory]
    [InlineData(null, null, "Existing company", "Existing pitch")]
    [InlineData("", "", "", "")]
    public async Task Put_CompanyFields_NullPreservesAndEmptyClears(string? companyName, string? companyPitch,
        string expectedName, string expectedPitch)
    {
        using var created = await Put(_userId, new
        {
            dailyLeadLimit = 30, companyName = "Existing company", companyPitch = "Existing pitch"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var updated = await Put(_userId, new { dailyLeadLimit = 30, companyName, companyPitch });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var get = await Send(HttpMethod.Get, Route, _userId);
        var settings = (await get.Content.ReadFromJsonAsync<ApiResponse<UserSettingsDto>>())!.Data!;
        Assert.Equal(expectedName, settings.CompanyName);
        Assert.Equal(expectedPitch, settings.CompanyPitch);
    }

    [Fact]
    public async Task Put_CompanyNameOver100Characters_ReturnsValidationErrorWithoutChangingSettings()
    {
        var validName = new string('x', 100);
        using var created = await Put(_userId, new
        {
            dailyLeadLimit = 30, companyName = validName, companyPitch = "Existing pitch"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var updated = await Put(_userId, new
        {
            dailyLeadLimit = 40, companyName = new string('x', 101), companyPitch = "Changed pitch"
        });
        Assert.Equal(HttpStatusCode.BadRequest, updated.StatusCode);
        var error = await updated.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains("CompanyName must be 100 characters or fewer.", error!.Message);
        await WithDb(async db =>
        {
            var setting = await db.UserSettings.SingleAsync();
            Assert.Equal(validName, setting.CompanyName);
            Assert.Equal("Existing pitch", setting.CompanyPitch);
            Assert.Equal(30, setting.DailyMessageLimit);
        });
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Anonymous_IsUnauthorized(string method)
    {
        using var response = await Send(new HttpMethod(method), Route, null, "{\"dailyLeadLimit\":10}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await WithDb(async db => Assert.Empty(await db.UserSettings.ToListAsync()));
    }

    [Theory]
    [InlineData("GET", "Admin")]
    [InlineData("PUT", "Admin")]
    [InlineData("GET", "SuperAdmin")]
    [InlineData("PUT", "SuperAdmin")]
    [InlineData("GET", "Worker")]
    [InlineData("PUT", "Worker")]
    public async Task NonUserRole_IsForbidden(string method, string role)
    {
        using var response = await Send(new HttpMethod(method), Route, _userId, "{\"dailyLeadLimit\":10}", role);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await WithDb(async db => Assert.Empty(await db.UserSettings.ToListAsync()));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task InvalidatedSession_IsUnauthorized(string method)
    {
        using var response = await Send(new HttpMethod(method), Route, _userId, "{\"dailyLeadLimit\":10}", session: "old-session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task EmptyUserIdentity_IsUnauthorized(string method)
    {
        using var response = await Send(new HttpMethod(method), Route, Guid.Empty, "{\"dailyLeadLimit\":10}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"dailyLeadLimit\":-1}")]
    [InlineData("{}")]
    [InlineData("{\"dailyLeadLimit\":null}")]
    [InlineData("{\"dailyLeadLimit\":\"invalid\"}")]
    [InlineData("{\"dailyLeadLimit\":1.5}")]
    [InlineData("{\"dailyLeadLimit\":2147483648}")]
    [InlineData("{\"dailyLeadLimit\":10,\"companyName\":{}}")]
    [InlineData("{\"dailyLeadLimit\":10,\"companyPitch\":123}")]
    [InlineData("{\"dailyLeadLimit\":10,\"aiApiKeyEncrypted\":\"forbidden field\"}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task InvalidBody_ReturnsStandardErrorAndDoesNotChangeSettings(string body)
    {
        await Put(_userId, new { dailyLeadLimit = 25 });
        using var response = await Send(HttpMethod.Put, Route, _userId, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.False(error!.IsSuccess);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal(Route, error.Path);
        Assert.Equal("PUT", error.Method);
        Assert.False(string.IsNullOrEmpty(error.TraceId));
        Assert.Null(error.Details);
        if (body.Contains("-1"))
            Assert.Contains("DailyLeadLimit must be greater than or equal to 0.", error.Message);
        await WithDb(async db => Assert.Equal(25, (await db.UserSettings.SingleAsync()).DailyMessageLimit));
    }

    [Fact]
    public async Task Put_UserIdInBody_IsRejectedAndCannotModifyAnotherUser()
    {
        await Put(_otherUserId, new { dailyLeadLimit = 90 });
        using var response = await Put(_userId, new { userId = _otherUserId, dailyLeadLimit = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await WithDb(async db =>
        {
            var setting = Assert.Single(await db.UserSettings.ToListAsync());
            Assert.Equal(_otherUserId, setting.UserId);
            Assert.Equal(90, setting.DailyMessageLimit);
        });
    }

    private Task<HttpResponseMessage> Put(Guid userId, object body) =>
        Send(HttpMethod.Put, Route, userId, JsonSerializer.Serialize(body));

    private async Task<HttpResponseMessage> Send(HttpMethod method, string route, Guid? userId,
        string? body = null, string role = "User", string session = Session)
    {
        using var request = new HttpRequestMessage(method, route);
        if (userId.HasValue)
        {
            var token = new JwtSecurityToken(_security.JwtIssuer, _security.JwtAudience,
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                 new Claim(ClaimTypes.Role, role), new Claim("SessionToken", session)],
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_security.JwtSecret)), SecurityAlgorithms.HmacSha256));
            request.Headers.Add("Cookie", $"{TokenService.AccessTokenCookieName}={new JwtSecurityTokenHandler().WriteToken(token)}");
        }
        if (body is not null && method != HttpMethod.Get)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await _client.SendAsync(request);
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = _host.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

}
