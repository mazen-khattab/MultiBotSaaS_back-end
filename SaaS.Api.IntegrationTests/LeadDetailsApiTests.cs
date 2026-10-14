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

// Uses production routing, JWT cookies, session validation, CQRS and relational EF mappings.
public sealed class LeadDetailsApiTests : IAsyncLifetime
{
    private const string Route = "/api/v1/leads";
    private const string Session = "lead-details-test-session";
    private const long OwnedLeadId = 3_000_000_001;
    private const string Metadata = "{\"bio\":\"Consultant\",\"context\":{\"comment\":\"Interested\"}}";
    private static readonly DateTime CreatedAt = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly SecuritySettings _security = new()
    {
        JwtSecret = "lead-details-integration-signing-key-at-least-32-bytes",
        JwtIssuer = "lead-details-tests", JwtAudience = "lead-details-tests"
    };
    private IHost _host = null!;
    private HttpClient _client = null!;
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _host = await new HostBuilder().ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHost(web => web.UseTestServer().UseEnvironment("Production")
                .ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(LeadsController).Assembly);
                    services.AddJwtAuthentication(_security);
                    services.AddAuthorization();
                    services.AddMediatRWithValidation();
                    services.AddScoped<AppDbContext>(_ => new SqliteAppDbContext(
                        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options));
                    services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
                    services.AddScoped<IUserBotService, UserBotService>();
                    services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
                    services.Configure<GeneralSettings>(_ => { });
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
            db.Users.AddRange(new User
                {
                    Id = _userId, Email = "owner@example.test", CurrentSessionToken = Session,
                    PasswordHash = "test-password-hash-never-expose"
                },
                new User { Id = _otherUserId, Email = "other@example.test", CurrentSessionToken = Session });
            db.Bots.AddRange(new Bot { Id = 100, Name = "Shared bot", UiModuleCode = "SHARED" },
                new Bot { Id = 200, Name = "Other bot", UiModuleCode = "OTHER" });
            db.UserBots.AddRange(new UserBot { UserId = _userId, BotId = 100 },
                new UserBot { UserId = _otherUserId, BotId = 100 },
                new UserBot { UserId = _otherUserId, BotId = 200 });
            db.TargetGroups.Add(new TargetGroup { Id = 100, UserId = _userId, BotId = 100, GroupName = "Test group" });
            db.ConnectedAccounts.Add(new ConnectedAccount
            {
                Id = 100, UserId = _userId, BotId = 100, DisplayName = "Test account",
                Cookie = new ConnectedAccountCookie { EncryptedCookies = "test-encrypted-cookie-never-expose" }
            });
            db.Scrapes.Add(new Scrape { Id = 100, UserId = _userId, BotId = 100, GroupId = 100, AccountId = 100 });
            db.Leads.AddRange(new Lead
                {
                    Id = OwnedLeadId, UserId = _userId, BotId = 100, GroupId = 100, AccountId = 100, ScrapeId = 100,
                    ExternalId = "external-profile-123", ProfileName = "Test Lead", ProfileUrl = "https://example.test/profile/123",
                    AiMessage = "Hello from the assistant.", Status = "Completed", CreatedAt = CreatedAt,
                    ProcessedAt = CreatedAt.AddMinutes(5), Detail = new LeadDetail { MetaDataJson = Metadata }
                },
                new Lead { Id = 102, UserId = _otherUserId, BotId = 100, ProfileName = "Other user's lead" },
                new Lead { Id = 103, UserId = _userId, BotId = 200, ProfileName = "Unassigned bot lead" },
                new Lead { Id = 104, UserId = _userId, BotId = null, ProfileName = "Historical lead" },
                new Lead { Id = 105, UserId = _userId, BotId = 100, ProfileName = "Pending lead" });
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
    public async Task OwnedLead_ReturnsDedicatedDtoWithMetadataAndNoNavigationOrSecurityFields()
    {
        using var response = await Send($"{Route}/{OwnedLeadId}", _userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>();
        Assert.True(result!.IsSuccess);
        Assert.Equal("Lead retrieved successfully.", result.Message);
        Assert.Equal(new LeadDetailsDto(OwnedLeadId, 100, 100, 100, 100, "external-profile-123", "Test Lead",
            "https://example.test/profile/123", "Hello from the assistant.", "Completed", CreatedAt,
            CreatedAt.AddMinutes(5), Metadata), result.Data);

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json).GetProperty("data");
        var expectedFields = new[] { "id", "botId", "groupId", "accountId", "scrapeId", "externalId", "profileName",
            "profileUrl", "aiMessage", "status", "createdAt", "processedAt", "metadataJson" };
        Assert.Equal(expectedFields.OrderBy(name => name), data.EnumerateObject().Select(field => field.Name).OrderBy(name => name));
        Assert.DoesNotContain(Session, json);
        Assert.DoesNotContain("test-password-hash-never-expose", json);
        Assert.DoesNotContain("test-encrypted-cookie-never-expose", json);
    }

    [Fact]
    public async Task NullableFields_ArePreservedWithoutInventingValues()
    {
        using var response = await Send($"{Route}/105", _userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lead = (await response.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>())!.Data!;
        Assert.Equal(105, lead.Id);
        Assert.Null(lead.GroupId);
        Assert.Null(lead.AccountId);
        Assert.Null(lead.ScrapeId);
        Assert.Null(lead.AiMessage);
        Assert.Null(lead.ProcessedAt);
        Assert.Equal("Pending", lead.Status);
        // The existing split-table metadata column defaults to {} when no detail is supplied.
        Assert.Equal("{}", lead.MetadataJson);
    }

    [Fact]
    public async Task AnotherUsersLead_WithSharedBot_ReturnsSameNotFoundAsMissingLead()
    {
        using var other = await Send($"{Route}/102", _userId);
        await AssertNotFound(other, $"{Route}/102");
        using var missing = await Send($"{Route}/999", _userId);
        await AssertNotFound(missing, $"{Route}/999");

        using var actualOwner = await Send($"{Route}/102", _otherUserId);
        Assert.Equal(HttpStatusCode.OK, actualOwner.StatusCode);
        Assert.DoesNotContain("Other user's lead", await other.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(999L)]
    [InlineData(long.MaxValue)]
    public async Task MissingLead_ReturnsStandardNotFound(long id)
    {
        using var response = await Send($"{Route}/{id}", _userId);
        await AssertNotFound(response, $"{Route}/{id}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuppliedUserIdAndBotId_DoNotChangeAuthenticatedOwnership(bool ownLead)
    {
        var path = $"{Route}/{(ownLead ? OwnedLeadId : 102)}?userId={_otherUserId}&botId=200";
        using var response = await Send(path, _userId);
        Assert.Equal(ownLead ? HttpStatusCode.OK : HttpStatusCode.NotFound, response.StatusCode);
        if (ownLead)
            Assert.Equal(OwnedLeadId, (await response.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>())!.Data!.Id);
    }

    [Fact]
    public async Task RemovedAssignment_RevokesAccessToLinkedLeads()
    {
        await WithDb(async db =>
        {
            db.UserBots.Remove(await db.UserBots.SingleAsync(assignment => assignment.UserId == _userId && assignment.BotId == 100));
            await db.SaveChangesAsync();
        });
        using var response = await Send($"{Route}/{OwnedLeadId}", _userId);
        await AssertNotFound(response, $"{Route}/{OwnedLeadId}");
    }

    [Fact]
    public async Task LeadLinkedToUnassignedBot_ReturnsNotFound()
    {
        using var response = await Send($"{Route}/103", _userId);
        await AssertNotFound(response, $"{Route}/103");
    }

    [Fact]
    public async Task HistoricalLeadWithoutBot_RemainsScopedToItsOwner()
    {
        using var own = await Send($"{Route}/104", _userId);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var lead = (await own.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>())!.Data!;
        Assert.Equal(104, lead.Id);
        Assert.Null(lead.BotId);
        using var other = await Send($"{Route}/104", _otherUserId);
        await AssertNotFound(other, $"{Route}/104");
    }

    [Fact]
    public async Task AnonymousAndInvalidCookie_ReturnUnauthorized()
    {
        using var anonymous = await Send($"{Route}/{OwnedLeadId}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Route}/{OwnedLeadId}");
        request.Headers.Add("Cookie", $"{TokenService.AccessTokenCookieName}=invalid-token");
        using var invalid = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleOrDeactivatedSession_ReturnsUnauthorized(bool deactivated)
    {
        await WithDb(async db =>
        {
            var user = await db.Users.SingleAsync(candidate => candidate.Id == _userId);
            user.IsDeleted = deactivated;
            user.CurrentSessionToken = deactivated ? Session : "new-session";
            await db.SaveChangesAsync();
        });
        using var response = await Send($"{Route}/{OwnedLeadId}", _userId);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveId_ReturnsValidationError(long id)
    {
        using var response = await Send($"{Route}/{id}", _userId);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(400, error!.StatusCode);
        Assert.Contains("LeadId must be greater than 0.", error.Message);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("9223372036854775808")]
    public async Task InvalidLongRoute_DoesNotMatch(string id)
    {
        using var response = await Send($"{Route}/{id}", _userId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExistingListAndMessagingPreviewRoutes_RemainAvailableAndScoped()
    {
        using var list = await Send($"{Route}/leads/100", _userId);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content.ReadFromJsonAsync<ApiResponse<PaginatedResult<LeadListDto>>>())!.Data!;
        Assert.Equal(new[] { OwnedLeadId, 105L }.OrderBy(id => id), page.Items.Select(lead => lead.LeadId).OrderBy(id => id));
        using var preview = await Send($"{Route}/bot/messaging-preview/100", _userId);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var leads = (await preview.Content.ReadFromJsonAsync<ApiResponse<MessagingPreviewResponseDto>>())!.Data!.Leads;
        Assert.Equal(105, Assert.Single(leads).LeadId);
    }

    private static async Task AssertNotFound(HttpResponseMessage response, string path)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.False(error!.IsSuccess);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal("Lead not found.", error.Message);
        Assert.Equal(path, error.Path);
        Assert.Equal("GET", error.Method);
        Assert.False(string.IsNullOrEmpty(error.TraceId));
        Assert.Null(error.Details);
        Assert.Null(error.InnerException);
    }

    private async Task<HttpResponseMessage> Send(string path, Guid? userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (userId.HasValue)
        {
            var token = new JwtSecurityToken(_security.JwtIssuer, _security.JwtAudience,
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()), new Claim(ClaimTypes.Role, "User"),
                 new Claim("SessionToken", Session)], expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_security.JwtSecret)),
                    SecurityAlgorithms.HmacSha256));
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
