using System.Net;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Api.IntegrationTests;

public sealed class ClientAuthorizationApiTests : AuthorizationApiTestBase
{
    public static IEnumerable<object[]> ProtectedRoutes()
    {
        var routes = new[]
        {
            ("GET", "/api/v1/auth/me"), ("POST", "/api/v1/auth/logout"),
            ("GET", "/api/v1/bots"), ("GET", "/api/v1/bots/subscribed"), ("GET", "/api/v1/bots/1"),
            ("GET", "/api/v1/connectedaccounts/1"), ("GET", "/api/v1/connectedaccounts/1/11"),
            ("GET", "/api/v1/connectedaccounts/eligible/1"), ("POST", "/api/v1/connectedaccounts"),
            ("PUT", "/api/v1/connectedaccounts/11"), ("DELETE", "/api/v1/connectedaccounts/11"),
            ("GET", "/api/v1/targetgroups/1"), ("GET", "/api/v1/targetgroups/1/21"),
            ("POST", "/api/v1/targetgroups"), ("PUT", "/api/v1/targetgroups/21"), ("DELETE", "/api/v1/targetgroups/21"),
            ("GET", "/api/v1/scrapes?botId=1"), ("GET", "/api/v1/scrapes/41"),
            ("POST", "/api/v1/scrapes"), ("POST", "/api/v1/scrapes/41/cancel"),
            ("GET", "/api/v1/leads/leads/1"), ("GET", "/api/v1/leads/bot/messaging-preview/1"), ("GET", "/api/v1/leads/31"),
            ("GET", "/api/v1/settings"), ("PUT", "/api/v1/settings"), ("GET", "/api/v1/activity-logs"),
            ("POST", "/api/v1/worker/dispatch-messaging")
        };
        foreach (var (method, route) in routes)
            foreach (var state in new[] { "anonymous", "deleted", "stale" })
                yield return [method, route, state];
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedEndpoints_RejectAnonymousDeletedAndStaleSessions(string method, string route, string state)
    {
        if (state == "deleted")
            await WithDb(async db => { (await db.Users.FindAsync(UserA))!.IsDeleted = true; await db.SaveChangesAsync(); });
        var cookie = state == "anonymous" ? null : AccessCookie(UserA, session: state == "stale" ? "old-session" : SessionA);
        using var response = await Send(method, route, new { }, cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/bots")]
    [InlineData("/api/v1/bots/subscribed")]
    [InlineData("/api/v1/bots/1?requireOwnership=true")]
    [InlineData("/api/v1/connectedaccounts/1")]
    [InlineData("/api/v1/connectedaccounts/eligible/1")]
    [InlineData("/api/v1/connectedaccounts/1/11")]
    [InlineData("/api/v1/targetgroups/1")]
    [InlineData("/api/v1/targetgroups/1/21")]
    [InlineData("/api/v1/scrapes?botId=1")]
    [InlineData("/api/v1/leads/leads/1")]
    [InlineData("/api/v1/leads/bot/messaging-preview/1")]
    [InlineData("/api/v1/leads/31")]
    [InlineData("/api/v1/settings")]
    [InlineData("/api/v1/activity-logs")]
    public async Task ValidUser_ReadContractsAreSuccessfulAndDoNotExposeCredentials(string route)
    {
        using var response = await Send("GET", route, cookie: AccessCookie(UserA));
        await SuccessData(response);
        var json = await response.Content.ReadAsStringAsync();
        foreach (var sensitive in new[] { "passwordHash", "encryptedCookies", "currentSessionToken", "refreshToken", "twoFactorSecret",
                     "connected-account-credential-secret", SessionA, SessionB, "aIApiKeyEncrypted", "scraperApiTokenEncrypted" })
            Assert.DoesNotContain(sensitive, json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("GET", "connectedaccounts", "1/12")]
    [InlineData("PUT", "connectedaccounts", "12")]
    [InlineData("DELETE", "connectedaccounts", "12")]
    [InlineData("GET", "targetgroups", "1/22")]
    [InlineData("PUT", "targetgroups", "22")]
    [InlineData("DELETE", "targetgroups", "22")]
    public async Task ForeignAccountAndGroup_ReadUpdateDeleteAreNotFoundAndDoNotMutate(string method, string resource, string suffix)
    {
        object body = resource == "connectedaccounts"
            ? new UpdateConnectedAccountDto("Changed", "Facebook", "{}", "Active", true)
            : new UpdateGroupDto("Changed", "https://example.test/changed", "{}", false);
        using var response = await Send(method, $"/api/v1/{resource}/{suffix}?userId={UserB}", body, AccessCookie(UserA));
        await Error(response, 404);
        await WithDb(async db =>
        {
            Assert.Equal("Account 12", (await db.ConnectedAccounts.FindAsync(12))!.DisplayName);
            Assert.Equal("Other group", (await db.TargetGroups.FindAsync(22))!.GroupName);
        });
    }

    [Fact]
    public async Task OwnAccountAndGroup_CreateUpdateDeleteRemainSuccessfulAndServerOwned()
    {
        using var account = await Send("POST", "/api/v1/connectedaccounts", new
        {
            botId = 1, displayName = "Created account", platform = "Facebook", cookiesPayload = CookiesPayload, userId = UserB
        }, AccessCookie(UserA));
        await SuccessData(account);
        int accountId = 0;
        await WithDb(async db =>
        {
            var stored = await db.ConnectedAccounts.Include(value => value.Cookie).SingleAsync(value => value.DisplayName == "Created account");
            Assert.Equal(UserA, stored.UserId); Assert.DoesNotContain(CookiesPayload, stored.Cookie.EncryptedCookies);
            accountId = stored.Id;
        });
        using var updateAccount = await Send("PUT", $"/api/v1/connectedaccounts/{accountId}",
            new UpdateConnectedAccountDto("Edited", "Facebook", CookiesPayload, "Active", true), AccessCookie(UserA));
        await SuccessData(updateAccount);
        using var deleteAccount = await Send("DELETE", $"/api/v1/connectedaccounts/{accountId}", cookie: AccessCookie(UserA));
        await SuccessData(deleteAccount);

        using var group = await Send("POST", "/api/v1/targetgroups", new
        {
            botId = 1, groupName = "Created group", groupUrl = "https://example.test/group", configJson = "{}", isActive = true, userId = UserB
        }, AccessCookie(UserA));
        int groupId = (await SuccessData(group)).GetInt32();
        using var updateGroup = await Send("PUT", $"/api/v1/targetgroups/{groupId}",
            new UpdateGroupDto("Edited", "https://example.test/edited", "{}", false), AccessCookie(UserA));
        await SuccessData(updateGroup);
        await WithDb(async db => Assert.Equal(UserA, (await db.TargetGroups.FindAsync(groupId))!.UserId));
        using var deleteGroup = await Send("DELETE", $"/api/v1/targetgroups/{groupId}", cookie: AccessCookie(UserA));
        await SuccessData(deleteGroup);
    }

    [Theory]
    [InlineData("/api/v1/connectedaccounts/1", "id", 11)]
    [InlineData("/api/v1/connectedaccounts/eligible/1", "id", 11)]
    [InlineData("/api/v1/targetgroups/1", "id", 21)]
    public async Task SharedBotLists_ContainOnlyAuthenticatedUserResources(string route, string idField, int expectedId)
    {
        using var response = await Send("GET", route + $"?userId={UserB}", cookie: AccessCookie(UserA));
        var items = (await SuccessData(response)).EnumerateArray().ToList();
        Assert.Equal(expectedId, Assert.Single(items).GetProperty(idField).GetInt32());
    }

    [Fact]
    public async Task BotCatalogAndSubscriptions_ArePersonalizedWithoutHidingTheSharedCatalog()
    {
        using var response = await Send("GET", $"/api/v1/bots?userId={UserB}", cookie: AccessCookie(UserA));
        var bots = (await SuccessData(response)).EnumerateArray().ToList();
        Assert.Equal(3, bots.Count);
        Assert.False(bots.Single(bot => bot.GetProperty("id").GetInt32() == 2).GetProperty("isSubscribed").GetBoolean());
        using var subscribed = await Send("GET", $"/api/v1/bots/subscribed?userId={UserB}", cookie: AccessCookie(UserA));
        Assert.Equal(new[] { 1, 3 }, (await SuccessData(subscribed)).EnumerateArray().Select(bot => bot.GetProperty("id").GetInt32()));
        using var restricted = await Send("GET", "/api/v1/bots/2?requireOwnership=true", cookie: AccessCookie(UserA));
        await Error(restricted, 404);
    }

    [Theory]
    [InlineData("/api/v1/connectedaccounts/eligible/2")]
    [InlineData("/api/v1/connectedaccounts/2")]
    [InlineData("/api/v1/targetgroups/2")]
    [InlineData("/api/v1/scrapes?botId=2")]
    [InlineData("/api/v1/leads/leads/2")]
    [InlineData("/api/v1/leads/bot/messaging-preview/2")]
    public async Task SubscriptionRequiredReads_RejectUnassignedBot(string route)
    {
        using var response = await Send("GET", route, cookie: AccessCookie(UserA));
        await Error(response, 404);
    }
}
