using System.Collections.Concurrent;
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
    private const string DetailsRoute = Route + "/LeadDetails";
    private const string RemoveRoute = Route + "/RemoveLead";
    private const string UpdateRoute = Route + "/UpdateLead";
    private static readonly UpdateLeadDto UpdatedLead = new("Updated Lead", "https://example.test/updated-profile",
        "Updated AI message", "Failed");
    private const string Session = "lead-details-test-session";
    private const long OwnedLeadId = 3_000_000_001;
    private const string Metadata = "{\"bio\":\"Consultant\",\"context\":{\"comment\":\"Interested\"}}";
    private static readonly DateTime CreatedAt = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly ConcurrentQueue<string> _logs = new();
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
        _host = await new HostBuilder().ConfigureLogging(logging => logging.ClearProviders()
                .SetMinimumLevel(LogLevel.Information).AddProvider(new CaptureLoggerProvider(_logs)))
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
                new Lead
                {
                    Id = 102, UserId = _otherUserId, BotId = 100, ProfileName = "Other user's lead",
                    Detail = new LeadDetail { MetaDataJson = "{\"bio\":\"Other user's metadata\"}" }
                },
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
        using var response = await Send($"{DetailsRoute}/{OwnedLeadId}", _userId);
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
        Assert.Contains(_logs, entry => entry.Contains($"Retrieved details for LeadId {OwnedLeadId} for UserId {_userId}"));
        Assert.DoesNotContain(_logs, entry => entry.Contains(Metadata) || entry.Contains(Session));
    }

    [Fact]
    public async Task NullableFields_ArePreservedWithoutInventingValues()
    {
        using var response = await Send($"{DetailsRoute}/105", _userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lead = (await response.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>())!.Data!;
        Assert.Equal(105, lead.Id);
        Assert.Null(lead.GroupId);
        Assert.Null(lead.AccountId);
        Assert.Null(lead.ScrapeId);
        Assert.Null(lead.AiMessage);
        Assert.Null(lead.ProcessedAt);
        Assert.Equal("Pending", lead.Status);
        // A lead can exist without a row in LeadDetails.
        Assert.Null(lead.MetadataJson);
    }

    [Fact]
    public async Task AnotherUsersLead_WithSharedBot_ReturnsSameNotFoundAsMissingLead()
    {
        using var other = await Send($"{DetailsRoute}/102", _userId);
        await AssertNotFound(other, $"{DetailsRoute}/102");
        using var missing = await Send($"{DetailsRoute}/999", _userId);
        await AssertNotFound(missing, $"{DetailsRoute}/999");

        using var actualOwner = await Send($"{DetailsRoute}/102", _otherUserId);
        Assert.Equal(HttpStatusCode.OK, actualOwner.StatusCode);
        Assert.DoesNotContain("Other user's lead", await other.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(999L)]
    [InlineData(long.MaxValue)]
    public async Task MissingLead_ReturnsStandardNotFound(long id)
    {
        using var response = await Send($"{DetailsRoute}/{id}", _userId);
        await AssertNotFound(response, $"{DetailsRoute}/{id}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuppliedUserIdAndBotId_DoNotChangeAuthenticatedOwnership(bool ownLead)
    {
        var path = $"{DetailsRoute}/{(ownLead ? OwnedLeadId : 102)}?userId={_otherUserId}&botId=200";
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
        using var response = await Send($"{DetailsRoute}/{OwnedLeadId}", _userId);
        await AssertNotFound(response, $"{DetailsRoute}/{OwnedLeadId}");
    }

    [Fact]
    public async Task LeadLinkedToUnassignedBot_ReturnsNotFound()
    {
        using var response = await Send($"{DetailsRoute}/103", _userId);
        await AssertNotFound(response, $"{DetailsRoute}/103");
    }

    [Fact]
    public async Task HistoricalLeadWithoutBot_RemainsScopedToItsOwner()
    {
        using var own = await Send($"{DetailsRoute}/104", _userId);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var lead = (await own.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>())!.Data!;
        Assert.Equal(104, lead.Id);
        Assert.Null(lead.BotId);
        using var other = await Send($"{DetailsRoute}/104", _otherUserId);
        await AssertNotFound(other, $"{DetailsRoute}/104");
    }

    [Theory]
    [InlineData("GET", DetailsRoute)]
    [InlineData("DELETE", RemoveRoute)]
    [InlineData("PUT", UpdateRoute)]
    public async Task AnonymousAndInvalidCookie_ReturnUnauthorized(string method, string route)
    {
        using var anonymous = await Send($"{route}/{OwnedLeadId}", null, new HttpMethod(method));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"{route}/{OwnedLeadId}");
        request.Headers.Add("Cookie", $"{TokenService.AccessTokenCookieName}=invalid-token");
        using var invalid = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Theory]
    [InlineData(false, "GET", DetailsRoute)]
    [InlineData(true, "GET", DetailsRoute)]
    [InlineData(false, "DELETE", RemoveRoute)]
    [InlineData(true, "DELETE", RemoveRoute)]
    [InlineData(false, "PUT", UpdateRoute)]
    [InlineData(true, "PUT", UpdateRoute)]
    public async Task StaleOrDeactivatedSession_ReturnsUnauthorized(bool deactivated, string method, string route)
    {
        await WithDb(async db =>
        {
            var user = await db.Users.SingleAsync(candidate => candidate.Id == _userId);
            user.IsDeleted = deactivated;
            user.CurrentSessionToken = deactivated ? Session : "new-session";
            await db.SaveChangesAsync();
        });
        using var response = await Send($"{route}/{OwnedLeadId}", _userId, new HttpMethod(method));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(0, "GET", DetailsRoute)]
    [InlineData(-1, "GET", DetailsRoute)]
    [InlineData(0, "DELETE", RemoveRoute)]
    [InlineData(-1, "DELETE", RemoveRoute)]
    [InlineData(0, "PUT", UpdateRoute)]
    [InlineData(-1, "PUT", UpdateRoute)]
    public async Task NonPositiveId_ReturnsValidationError(long id, string method, string route)
    {
        using var response = await Send($"{route}/{id}", _userId, new HttpMethod(method));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(400, error!.StatusCode);
        Assert.Contains("LeadId must be greater than 0.", error.Message);
    }

    [Theory]
    [InlineData("not-a-number", "GET", DetailsRoute)]
    [InlineData("9223372036854775808", "GET", DetailsRoute)]
    [InlineData("not-a-number", "DELETE", RemoveRoute)]
    [InlineData("9223372036854775808", "DELETE", RemoveRoute)]
    [InlineData("not-a-number", "PUT", UpdateRoute)]
    [InlineData("9223372036854775808", "PUT", UpdateRoute)]
    public async Task InvalidLongRoute_DoesNotMatch(string id, string method, string route)
    {
        using var response = await Send($"{route}/{id}", _userId, new HttpMethod(method));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExistingDetailsRoute_RemainsAvailableAndScopedToOwner()
    {
        using var owned = await Send($"{Route}/{OwnedLeadId}", _userId);
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
        Assert.Equal(Metadata, (await owned.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>())!.Data!.MetadataJson);
        using var other = await Send($"{Route}/102", _userId);
        await AssertNotFound(other, $"{Route}/102");
    }

    [Fact]
    public async Task RemoveOwnedLead_DeletesDetailsAndLeavesOtherUsersDataIntact()
    {
        var path = $"{RemoveRoute}/{OwnedLeadId}";
        using var response = await Send(path, _userId, HttpMethod.Delete);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        Assert.True(result!.IsSuccess);
        Assert.True(result.Data);
        Assert.Equal("Lead removed successfully.", result.Message);

        await WithDb(async db =>
        {
            Assert.False(await db.Leads.AnyAsync(lead => lead.Id == OwnedLeadId));
            Assert.False(await db.LeadDetails.AnyAsync(detail => detail.LeadId == OwnedLeadId));
            Assert.True(await db.Leads.AnyAsync(lead => lead.Id == 102 && lead.UserId == _otherUserId));
            Assert.Equal("{\"bio\":\"Other user's metadata\"}",
                (await db.LeadDetails.SingleAsync(detail => detail.LeadId == 102)).MetaDataJson);
            Assert.Equal(4, await db.Leads.CountAsync());
        });
        using var repeated = await Send(path, _userId, HttpMethod.Delete);
        await AssertNotFound(repeated, path, "DELETE");
        using var details = await Send($"{DetailsRoute}/{OwnedLeadId}", _userId);
        await AssertNotFound(details, $"{DetailsRoute}/{OwnedLeadId}");
        Assert.Contains(_logs, entry => entry.Contains($"Removed LeadId {OwnedLeadId} and its details for UserId {_userId}"));
        Assert.DoesNotContain(_logs, entry => entry.Contains(Metadata) || entry.Contains(Session));
    }

    [Theory]
    [InlineData(102L)] // Another user's lead, even though both users share the bot.
    [InlineData(103L)] // Owned lead whose bot is not assigned to the authenticated user.
    [InlineData(999L)]
    [InlineData(long.MaxValue)]
    public async Task RemoveInaccessibleOrMissingLead_ReturnsSameNotFoundAndChangesNothing(long id)
    {
        var path = $"{RemoveRoute}/{id}";
        using var response = await Send($"{path}?userId={_otherUserId}&botId=200", _userId, HttpMethod.Delete);
        await AssertNotFound(response, path, "DELETE");
        await WithDb(async db =>
        {
            Assert.Equal(5, await db.Leads.CountAsync());
            Assert.Equal(2, await db.LeadDetails.CountAsync());
            Assert.Equal(Metadata, (await db.LeadDetails.SingleAsync(detail => detail.LeadId == OwnedLeadId)).MetaDataJson);
        });
        Assert.Contains(_logs, entry => entry.Contains($"Lead removal unavailable for LeadId {id} for UserId {_userId}"));
    }

    [Theory]
    [InlineData(104L)] // Historical lead with no bot.
    [InlineData(105L)] // Lead with no detail row.
    public async Task RemoveOwnedLeadWithoutDetails_Succeeds(long id)
    {
        using var response = await Send($"{RemoveRoute}/{id}?userId={_otherUserId}", _userId, HttpMethod.Delete);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithDb(async db => Assert.False(await db.Leads.AnyAsync(lead => lead.Id == id)));
    }

    [Fact]
    public async Task RemoveOtherUsersLead_SucceedsOnlyForItsActualOwner()
    {
        using var response = await Send($"{RemoveRoute}/102", _otherUserId, HttpMethod.Delete);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithDb(async db =>
        {
            Assert.False(await db.Leads.AnyAsync(lead => lead.Id == 102));
            Assert.False(await db.LeadDetails.AnyAsync(detail => detail.LeadId == 102));
            Assert.True(await db.LeadDetails.AnyAsync(detail => detail.LeadId == OwnedLeadId));
        });
    }

    [Fact]
    public async Task RemoveAfterBotAssignmentIsRevoked_ReturnsNotFoundAndPreservesDetails()
    {
        await WithDb(async db =>
        {
            db.UserBots.Remove(await db.UserBots.SingleAsync(assignment => assignment.UserId == _userId && assignment.BotId == 100));
            await db.SaveChangesAsync();
        });
        var path = $"{RemoveRoute}/{OwnedLeadId}";
        using var response = await Send(path, _userId, HttpMethod.Delete);
        await AssertNotFound(response, path, "DELETE");
        await WithDb(async db => Assert.True(await db.LeadDetails.AnyAsync(detail => detail.LeadId == OwnedLeadId)));
    }

    [Fact]
    public async Task GetRequestCannotRemoveLead()
    {
        using var response = await Send($"{RemoveRoute}/{OwnedLeadId}", _userId);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        await WithDb(async db => Assert.True(await db.LeadDetails.AnyAsync(detail => detail.LeadId == OwnedLeadId)));
    }

    [Fact]
    public async Task UpdateOwnedLead_ChangesOnlyEditableFieldsAndIgnoresSpoofedOwnershipAndDetails()
    {
        // Extra JSON properties must never let clients replace ownership, relationships or metadata.
        var body = new
        {
            UpdatedLead.ProfileName, UpdatedLead.ProfileUrl, UpdatedLead.AiMessage, UpdatedLead.Status,
            UserId = _otherUserId, LeadId = 102, BotId = 200, GroupId = 999, AccountId = 999, ScrapeId = 999,
            ExternalId = "tampered", CreatedAt = DateTime.MinValue, ProcessedAt = DateTime.MinValue,
            MetadataJson = "tampered", Detail = new { MetaDataJson = "tampered" }
        };
        using var response = await Send($"{UpdateRoute}/{OwnedLeadId}?userId={_otherUserId}", _userId, HttpMethod.Put, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        Assert.True(result!.IsSuccess);
        Assert.True(result.Data);
        Assert.Equal("Lead updated successfully.", result.Message);

        using var detailsResponse = await Send($"{DetailsRoute}/{OwnedLeadId}", _userId);
        var details = (await detailsResponse.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>())!.Data;
        Assert.Equal(new LeadDetailsDto(OwnedLeadId, 100, 100, 100, 100, "external-profile-123",
            UpdatedLead.ProfileName, UpdatedLead.ProfileUrl, UpdatedLead.AiMessage, UpdatedLead.Status,
            CreatedAt, CreatedAt.AddMinutes(5), Metadata), details);
        await WithDb(async db =>
        {
            Assert.Equal(_userId, (await db.Leads.SingleAsync(lead => lead.Id == OwnedLeadId)).UserId);
            Assert.Equal("Other user's lead", (await db.Leads.SingleAsync(lead => lead.Id == 102)).ProfileName);
            Assert.Equal(2, await db.LeadDetails.CountAsync());
        });
        Assert.Contains(_logs, entry => entry.Contains($"Updated LeadId {OwnedLeadId} for UserId {_userId}"));
        Assert.DoesNotContain(_logs, entry => entry.Contains(Metadata) || entry.Contains(UpdatedLead.AiMessage!) || entry.Contains(Session));
    }

    [Theory]
    [InlineData(102L)] // Sharing a bot does not grant access to another user's lead.
    [InlineData(103L)]
    [InlineData(999L)]
    [InlineData(long.MaxValue)]
    public async Task UpdateInaccessibleOrMissingLead_ReturnsNotFoundAndPreservesData(long id)
    {
        var path = $"{UpdateRoute}/{id}";
        using var response = await Send($"{path}?userId={_otherUserId}&botId=200", _userId, HttpMethod.Put);
        await AssertNotFound(response, path, "PUT");
        await WithDb(async db =>
        {
            Assert.Equal(5, await db.Leads.CountAsync());
            Assert.Equal(2, await db.LeadDetails.CountAsync());
            Assert.Equal("Test Lead", (await db.Leads.SingleAsync(lead => lead.Id == OwnedLeadId)).ProfileName);
            Assert.Equal("Other user's lead", (await db.Leads.SingleAsync(lead => lead.Id == 102)).ProfileName);
            Assert.Equal("Unassigned bot lead", (await db.Leads.SingleAsync(lead => lead.Id == 103)).ProfileName);
            Assert.Equal(Metadata, (await db.LeadDetails.SingleAsync(detail => detail.LeadId == OwnedLeadId)).MetaDataJson);
        });
        Assert.Contains(_logs, entry => entry.Contains($"Lead update unavailable for LeadId {id} for UserId {_userId}"));
    }

    [Theory]
    [InlineData(104L)]
    [InlineData(105L)]
    public async Task UpdateLeadWithoutDetails_NormalizesStatusAndAllowsClearingAiMessage(long id)
    {
        var body = UpdatedLead with { AiMessage = null, Status = "pending" };
        using var response = await Send($"{UpdateRoute}/{id}", _userId, HttpMethod.Put, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithDb(async db =>
        {
            var lead = await db.Leads.SingleAsync(lead => lead.Id == id);
            Assert.Equal(body.ProfileName, lead.ProfileName);
            Assert.Equal(body.ProfileUrl, lead.ProfileUrl);
            Assert.Null(lead.AiMessage);
            Assert.Equal("Pending", lead.Status);
            Assert.False(await db.LeadDetails.AnyAsync(detail => detail.LeadId == id));
            Assert.Equal(2, await db.LeadDetails.CountAsync());
        });
    }

    [Fact]
    public async Task UpdateOtherUsersLead_SucceedsForActualOwner()
    {
        using var response = await Send($"{UpdateRoute}/102", _otherUserId, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithDb(async db =>
        {
            var lead = await db.Leads.SingleAsync(lead => lead.Id == 102);
            Assert.Equal(_otherUserId, lead.UserId);
            Assert.Equal(UpdatedLead.ProfileName, lead.ProfileName);
            Assert.Equal("{\"bio\":\"Other user's metadata\"}",
                (await db.LeadDetails.SingleAsync(detail => detail.LeadId == 102)).MetaDataJson);
        });
    }

    [Fact]
    public async Task UpdateAfterBotAssignmentIsRevoked_ReturnsNotFound()
    {
        await WithDb(async db =>
        {
            db.UserBots.Remove(await db.UserBots.SingleAsync(assignment => assignment.UserId == _userId && assignment.BotId == 100));
            await db.SaveChangesAsync();
        });
        var path = $"{UpdateRoute}/{OwnedLeadId}";
        using var response = await Send(path, _userId, HttpMethod.Put);
        await AssertNotFound(response, path, "PUT");
        await WithDb(async db => Assert.Equal("Test Lead", (await db.Leads.SingleAsync(lead => lead.Id == OwnedLeadId)).ProfileName));
    }

    [Theory]
    [InlineData("empty-name")]
    [InlineData("long-name")]
    [InlineData("invalid-url")]
    [InlineData("long-url")]
    [InlineData("invalid-status")]
    [InlineData("numeric-status")]
    [InlineData("null-body")]
    public async Task UpdateWithInvalidPayload_ReturnsBadRequestWithoutChangingLead(string scenario)
    {
        object body = scenario switch
        {
            "empty-name" => UpdatedLead with { ProfileName = "" },
            "long-name" => UpdatedLead with { ProfileName = new string('n', 256) },
            "invalid-url" => UpdatedLead with { ProfileUrl = "javascript:alert(1)" },
            "long-url" => UpdatedLead with { ProfileUrl = "https://example.test/" + new string('p', 431) },
            "invalid-status" => UpdatedLead with { Status = "Unknown" },
            "numeric-status" => UpdatedLead with { Status = "1" },
            "null-body" => JsonSerializer.Deserialize<JsonElement>("null"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        using var response = await Send($"{UpdateRoute}/{OwnedLeadId}", _userId, HttpMethod.Put, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await WithDb(async db =>
        {
            var lead = await db.Leads.SingleAsync(lead => lead.Id == OwnedLeadId);
            Assert.Equal("Test Lead", lead.ProfileName);
            Assert.Equal("Completed", lead.Status);
            Assert.Equal(Metadata, (await db.LeadDetails.SingleAsync(detail => detail.LeadId == OwnedLeadId)).MetaDataJson);
        });
    }

    [Fact]
    public async Task GetRequestCannotUpdateLead()
    {
        using var response = await Send($"{UpdateRoute}/{OwnedLeadId}", _userId);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
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

    private static async Task AssertNotFound(HttpResponseMessage response, string path, string method = "GET")
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.False(error!.IsSuccess);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal("Lead not found.", error.Message);
        Assert.Equal(path, error.Path);
        Assert.Equal(method, error.Method);
        Assert.False(string.IsNullOrEmpty(error.TraceId));
        Assert.Null(error.Details);
        Assert.Null(error.InnerException);
    }

    private async Task<HttpResponseMessage> Send(string path, Guid? userId, HttpMethod? method = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (method == HttpMethod.Put)
            request.Content = JsonContent.Create(body ?? UpdatedLead);
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

    private sealed class CaptureLoggerProvider(ConcurrentQueue<string> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(entries);
        public void Dispose() { }

        private sealed class CaptureLogger(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => entries.Enqueue(formatter(state, exception));
        }
    }
}
