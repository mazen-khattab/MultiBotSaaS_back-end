using System.Net;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Api.IntegrationTests;

public sealed class ConnectedAccountTypeApiTests : AuthorizationApiTestBase
{
    [Theory]
    [InlineData("/api/v1/connectedaccounts/eligible/1", "Scraping", "Messaging")]
    [InlineData("/api/v1/connectedaccounts/eligible/messaging/1", "Messaging", "Scraping")]
    public async Task EligibleAccounts_ReturnOnlyReadyAccountsOfRequestedType(string route, string type, string otherType)
    {
        await WithDb(async db =>
        {
            // Disable the fixture's own accounts so the expected result comes from this scenario.
            foreach (var account in await db.ConnectedAccounts.Where(a => a.UserId == UserA && a.BotId == 1).ToListAsync())
                account.IsActive = false;

            ConnectedAccount CreateAccount(int id, string displayName) => new()
            {
                Id = id, UserId = UserA, BotId = 1, DisplayName = displayName,
                Platform = "Facebook", Type = type, IsActive = true, Status = "Active",
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = id, EncryptedCookies = "test-cookie", CookiesExpireDate = DateTime.UtcNow.AddDays(1)
                }
            };

            var ready = CreateAccount(101, "Z ready");
            var wrongType = CreateAccount(102, "Other type");
            wrongType.Type = otherType;
            var inactive = CreateAccount(103, "Inactive");
            inactive.IsActive = false;
            var busy = CreateAccount(104, "Busy");
            busy.Status = "Busy";
            var expired = CreateAccount(105, "Expired");
            expired.Cookie.CookiesExpireDate = DateTime.UtcNow.AddDays(-1);
            var missingCookies = CreateAccount(106, "Missing cookies");
            missingCookies.Cookie = null!;
            var otherUser = CreateAccount(107, "Other user");
            otherUser.UserId = UserB;
            var otherBot = CreateAccount(108, "Other bot");
            otherBot.BotId = 3;
            var firstReady = CreateAccount(109, "A ready");

            db.ConnectedAccounts.AddRange(ready, wrongType, inactive, busy, expired, missingCookies, otherUser, otherBot, firstReady);
            await db.SaveChangesAsync();
        });

        using var response = await Send("GET", route, cookie: AccessCookie(UserA));
        var accounts = (await SuccessData(response)).EnumerateArray().ToArray();

        Assert.Equal(new[] { 109, 101 }, accounts.Select(a => a.GetProperty("id").GetInt32()));
        Assert.All(accounts, account => Assert.Equal(type, account.GetProperty("type").GetString()));
    }

    [Theory]
    [InlineData("/api/v1/connectedaccounts/eligible/1")]
    [InlineData("/api/v1/connectedaccounts/eligible/messaging/1")]
    public async Task EligibleAccounts_RequireAuthentication(string route)
    {
        using var response = await Send("GET", route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/connectedaccounts/eligible/2", 404)]
    [InlineData("/api/v1/connectedaccounts/eligible/messaging/2", 404)]
    [InlineData("/api/v1/connectedaccounts/eligible/0", 400)]
    [InlineData("/api/v1/connectedaccounts/eligible/messaging/0", 400)]
    public async Task EligibleAccounts_RejectUnownedOrInvalidBots(string route, int expectedStatus)
    {
        using var response = await Send("GET", route, cookie: AccessCookie(UserA));

        await Error(response, expectedStatus);
    }

    [Theory]
    [InlineData("Scraping", "Messaging")]
    [InlineData("Messaging", "Scraping")]
    public async Task CreateUpdateAndRead_PersistAndReturnAccountType(string initialType, string updatedType)
    {
        using var created = await Send("POST", "/api/v1/connectedaccounts",
            new AddConnectedAccountDto(1, "Typed account", "Facebook", CookiesPayload, initialType), AccessCookie(UserA));
        await SuccessData(created);

        var accountId = 0;
        await WithDb(async db =>
        {
            var account = await db.ConnectedAccounts.SingleAsync(a => a.DisplayName == "Typed account");
            Assert.Equal(initialType, account.Type);
            accountId = account.Id;
        });

        await AssertReadType(accountId, initialType);

        using var updated = await Send("PUT", $"/api/v1/connectedaccounts/{accountId}",
            new UpdateConnectedAccountDto("Typed account", "Facebook", CookiesPayload, "Active", true, updatedType), AccessCookie(UserA));
        await SuccessData(updated);

        await WithDb(async db => Assert.Equal(updatedType, (await db.ConnectedAccounts.FindAsync(accountId))!.Type));
        await AssertReadType(accountId, updatedType);
    }

    private async Task AssertReadType(int accountId, string expectedType)
    {
        using var detailResponse = await Send("GET", $"/api/v1/connectedaccounts/1/{accountId}", cookie: AccessCookie(UserA));
        var details = await SuccessData(detailResponse);
        Assert.Equal(expectedType, details.GetProperty("type").GetString());

        var eligibleRoute = expectedType == "Scraping"
            ? "/api/v1/connectedaccounts/eligible/1"
            : "/api/v1/connectedaccounts/eligible/messaging/1";
        foreach (var route in new[] { "/api/v1/connectedaccounts/1", eligibleRoute })
        {
            using var response = await Send("GET", route, cookie: AccessCookie(UserA));
            var accounts = await SuccessData(response);
            var account = accounts.EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == accountId);
            Assert.Equal(expectedType, account.GetProperty("type").GetString());
        }

        var otherTypeRoute = expectedType == "Scraping"
            ? "/api/v1/connectedaccounts/eligible/messaging/1"
            : "/api/v1/connectedaccounts/eligible/1";
        using var otherTypeResponse = await Send("GET", otherTypeRoute, cookie: AccessCookie(UserA));
        var otherTypeAccounts = await SuccessData(otherTypeResponse);
        Assert.DoesNotContain(otherTypeAccounts.EnumerateArray(), a => a.GetProperty("id").GetInt32() == accountId);
    }

    [Theory]
    [InlineData("POST", null)]
    [InlineData("PUT", null)]
    [InlineData("POST", "Unknown")]
    [InlineData("PUT", "Unknown")]
    [InlineData("POST", "0")]
    [InlineData("PUT", "0")]
    public async Task InvalidType_ReturnsBadRequestWithoutChangingAccounts(string method, string? type)
    {
        var route = method == "POST" ? "/api/v1/connectedaccounts" : "/api/v1/connectedaccounts/11";
        using var response = await Send(method, route, new
        {
            botId = 1, displayName = "Invalid account", platform = "Facebook",
            cookiesPayload = CookiesPayload, status = "Active", isActive = true, type
        }, AccessCookie(UserA));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await WithDb(async db =>
        {
            Assert.False(await db.ConnectedAccounts.AnyAsync(a => a.DisplayName == "Invalid account"));
            Assert.Equal("Scraping", (await db.ConnectedAccounts.FindAsync(11))!.Type);
        });
    }
}
