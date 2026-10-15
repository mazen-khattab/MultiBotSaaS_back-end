using System.Net;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Api.IntegrationTests;

public sealed class RunAuthorizationApiTests : AuthorizationApiTestBase
{
    [Theory]
    [InlineData(false, "foreign-account", 404)]
    [InlineData(false, "foreign-group", 404)]
    [InlineData(false, "different-account-bot", 404)]
    [InlineData(false, "different-group-bot", 404)]
    [InlineData(false, "unassigned", 404)]
    [InlineData(false, "disabled", 404)]
    [InlineData(false, "expired-cookies", 422)]
    [InlineData(true, "foreign-account", 404)]
    [InlineData(true, "different-account-bot", 404)]
    [InlineData(true, "unassigned", 404)]
    [InlineData(true, "disabled", 404)]
    [InlineData(true, "expired-cookies", 422)]
    [InlineData(true, "foreign-lead", 422)]
    public async Task Start_RejectsUnownedUnassignedMismatchedAndIneligibleResourcesWithoutDispatch(bool messaging, string scenario, int status)
    {
        int botId = scenario == "unassigned" ? 2 : 1;
        int accountId = scenario == "foreign-account" ? 12 : scenario == "different-account-bot" ? 13 : scenario == "unassigned" ? 14 : 11;
        int? groupId = scenario == "foreign-group" ? 22 : scenario == "different-group-bot" ? 23 : null;
        long leadId = scenario == "foreign-lead" ? 32 : scenario == "unassigned" ? 34 : 31;
        await WithDb(async db =>
        {
            if (scenario == "unassigned") db.Leads.Add(new Lead { Id = 34, UserId = UserA, BotId = 2 });
            if (scenario == "disabled") (await db.ConnectedAccounts.FindAsync(11))!.IsActive = false;
            if (scenario == "expired-cookies") (await db.ConnectedAccountCookies.FindAsync(11))!.CookiesExpireDate = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });
        object body = messaging ? new DispatchMessagingDto(botId, accountId, [leadId]) : new CreateScrapeDto(botId, accountId, groupId, "{}");
        using var response = await Send("POST", messaging ? "/api/v1/worker/dispatch-messaging" : "/api/v1/scrapes", body, AccessCookie(UserA));
        await Error(response, status);
        await WithDb(async db =>
        {
            Assert.Empty(await db.Scrapes.ToListAsync()); Assert.Empty(await db.Jobs.ToListAsync());
            Assert.Equal("Active", (await db.ConnectedAccounts.FindAsync(accountId))!.Status);
        });
        Network.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, "Busy")]
    [InlineData(false, "CoolingDown")]
    [InlineData(false, "AccountFlagged")]
    [InlineData(false, "Banned")]
    [InlineData(true, "Busy")]
    [InlineData(true, "CoolingDown")]
    [InlineData(true, "AccountFlagged")]
    [InlineData(true, "Banned")]
    public async Task OnlyActiveStatusAccounts_AppearEligibleAndCanStart(bool messaging, string status)
    {
        await WithDb(async db => { (await db.ConnectedAccounts.FindAsync(11))!.Status = status; await db.SaveChangesAsync(); });
        using var eligible = await Send("GET", "/api/v1/connectedaccounts/eligible/1", cookie: AccessCookie(UserA));
        Assert.Empty((await SuccessData(eligible)).EnumerateArray());
        object body = messaging ? new DispatchMessagingDto(1, 11, [31]) : new CreateScrapeDto(1, 11, null, "{}");
        using var response = await Send("POST", messaging ? "/api/v1/worker/dispatch-messaging" : "/api/v1/scrapes", body, AccessCookie(UserA));
        await Error(response, 422);
        Network.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EligibleAccounts_ExcludeDisabledExpiredAndMissingCookies()
    {
        await WithDb(async db =>
        {
            var account = await db.ConnectedAccounts.Include(value => value.Cookie).SingleAsync(value => value.Id == 11);
            account.IsActive = false; await db.SaveChangesAsync();
        });
        using var disabled = await Send("GET", "/api/v1/connectedaccounts/eligible/1", cookie: AccessCookie(UserA));
        Assert.Empty((await SuccessData(disabled)).EnumerateArray());
        await WithDb(async db =>
        {
            (await db.ConnectedAccounts.FindAsync(11))!.IsActive = true;
            (await db.ConnectedAccountCookies.FindAsync(11))!.CookiesExpireDate = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        });
        using var expired = await Send("GET", "/api/v1/connectedaccounts/eligible/1", cookie: AccessCookie(UserA));
        Assert.Empty((await SuccessData(expired)).EnumerateArray());
        await WithDb(async db => { db.ConnectedAccountCookies.Remove((await db.ConnectedAccountCookies.FindAsync(11))!); await db.SaveChangesAsync(); });
        using var missing = await Send("GET", "/api/v1/connectedaccounts/eligible/1", cookie: AccessCookie(UserA));
        Assert.Empty((await SuccessData(missing)).EnumerateArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveOwnedSubscribedAccount_StartsAndPersistsOnlyAuthenticatedResources(bool messaging)
    {
        object body = messaging ? new DispatchMessagingDto(1, 11, [31]) : new CreateScrapeDto(1, 11, 21, "{}");
        using var response = await Send("POST", (messaging ? "/api/v1/worker/dispatch-messaging" : "/api/v1/scrapes") + $"?userId={UserB}", body, AccessCookie(UserA));
        await SuccessData(response);
        await WithDb(async db =>
        {
            if (messaging) { var job = Assert.Single(await db.Jobs.ToListAsync()); Assert.Equal(UserA, job.UserId); Assert.Equal(1, job.BotId); }
            else { var run = Assert.Single(await db.Scrapes.ToListAsync()); Assert.Equal(UserA, run.UserId); Assert.Equal(1, run.BotId); Assert.Equal(21, run.GroupId); }
            Assert.Equal("Busy", (await db.ConnectedAccounts.FindAsync(11))!.Status);
            Assert.Equal("Active", (await db.ConnectedAccounts.FindAsync(12))!.Status);
        });
        Assert.Single(Network.Invocations);
    }

    [Fact]
    public async Task Runs_ListDetailAndCancelAreOwnerScoped_AndCancelTransitionsOnlyRunningState()
    {
        await SeedRuns();
        using var list = await Send("GET", $"/api/v1/scrapes?botId=1&userId={UserB}", cookie: AccessCookie(UserA));
        var items = (await SuccessData(list)).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(41, Assert.Single(items).GetProperty("id").GetInt32());
        using var detail = await Send("GET", "/api/v1/scrapes/42", cookie: AccessCookie(UserA));
        await Error(detail, 404);
        using var cancelOther = await Send("POST", "/api/v1/scrapes/42/cancel", cookie: AccessCookie(UserA));
        await Error(cancelOther, 404);
        using var cancel = await Send("POST", "/api/v1/scrapes/41/cancel", cookie: AccessCookie(UserA));
        Assert.True((await SuccessData(cancel)).GetBoolean());
        using var repeat = await Send("POST", "/api/v1/scrapes/41/cancel", cookie: AccessCookie(UserA));
        await Error(repeat, 422);
        await WithDb(async db =>
        {
            var own = (await db.Scrapes.FindAsync(41))!;
            Assert.Equal("Cancelled", own.Status); Assert.NotNull(own.EndedAt);
            Assert.Equal("Running", (await db.Scrapes.FindAsync(42))!.Status);
            Assert.Equal("Active", (await db.ConnectedAccounts.FindAsync(11))!.Status);
            Assert.Equal("Busy", (await db.ConnectedAccounts.FindAsync(12))!.Status);
        });
    }

    [Fact]
    public async Task Cancel_OwnRunWithForeignAccountLink_DoesNotChangeAnotherUsersAccount()
    {
        await SeedRuns();
        await WithDb(async db => { (await db.Scrapes.FindAsync(41))!.AccountId = 12; await db.SaveChangesAsync(); });
        using var cancel = await Send("POST", "/api/v1/scrapes/41/cancel", cookie: AccessCookie(UserA));
        await SuccessData(cancel);
        await WithDb(async db => Assert.Equal("Busy", (await db.ConnectedAccounts.FindAsync(12))!.Status));
    }

    private Task SeedRuns() => WithDb(async db =>
    {
        db.Scrapes.AddRange(new Scrape { Id = 41, UserId = UserA, BotId = 1, AccountId = 11 },
            new Scrape { Id = 42, UserId = UserB, BotId = 1, AccountId = 12 });
        (await db.ConnectedAccounts.FindAsync(11))!.Status = "Busy";
        (await db.ConnectedAccounts.FindAsync(12))!.Status = "Busy";
        await db.SaveChangesAsync();
    });
}
