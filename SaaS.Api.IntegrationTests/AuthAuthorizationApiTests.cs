using System.Net;
using Microsoft.EntityFrameworkCore;
using SaaS.Domain.Entities;

namespace SaaS.Api.IntegrationTests;

public sealed class AuthAuthorizationApiTests : AuthorizationApiTestBase
{
    [Fact]
    public async Task UserLoginRefreshMeAndLogout_UseCookieOnlyContractsAndRevokePriorSession()
    {
        using var login = await Send("POST", "/api/v1/auth/login", new { email = "a@example.test", password = Password });
        var identity = await SuccessData(login);
        AssertIdentity(identity, UserA, "User");
        var firstCookies = ResponseCookies(login);
        await AssertCookiesAndNoTokenBody(login);
        using var me = await Send("GET", $"/api/v1/auth/me?userId={UserB}", cookie: firstCookies);
        AssertIdentity(await SuccessData(me), UserA, "User");
        Assert.False(me.Headers.Contains("Set-Cookie"));

        // Refresh must work after the access JWT expires; the refresh cookie is its credential.
        var oldRefresh = RefreshCookie(firstCookies);
        using var refreshed = await Send("POST", "/api/v1/auth/refresh", cookie:
            AccessCookie(UserA, expires: DateTime.UtcNow.AddMinutes(-1)) + "; " + oldRefresh);
        AssertIdentity(await SuccessData(refreshed), UserA, "User");
        await AssertCookiesAndNoTokenBody(refreshed);
        var currentCookies = ResponseCookies(refreshed);
        Assert.NotEqual(oldRefresh, RefreshCookie(currentCookies));
        using var replay = await Send("POST", "/api/v1/auth/refresh", cookie: oldRefresh);
        await Error(replay, 401);
        using var oldSession = await Send("GET", "/api/v1/auth/me", cookie: firstCookies);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        using var currentSession = await Send("GET", "/api/v1/auth/me", cookie: currentCookies);
        await SuccessData(currentSession);

        using var logout = await Send("POST", "/api/v1/auth/logout", cookie: currentCookies);
        var data = await SuccessData(logout);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, data.ValueKind);
        Assert.All(logout.Headers.GetValues("Set-Cookie"), value => { Assert.Contains("path=/", value); Assert.Contains("expires=", value); });
        using var endedSession = await Send("GET", "/api/v1/auth/me", cookie: currentCookies);
        Assert.Equal(HttpStatusCode.Unauthorized, endedSession.StatusCode);
        using var endedRefresh = await Send("POST", "/api/v1/auth/refresh", cookie: RefreshCookie(currentCookies));
        await Error(endedRefresh, 401);
        await WithDb(async db =>
        {
            Assert.Null((await db.Users.FindAsync(UserA))!.CurrentSessionToken);
            Assert.False(await db.UserRefreshTokens.AnyAsync(token => token.UserId == UserA && token.IsActive));
        });
    }

    [Theory]
    [InlineData("Admin", "admin@example.test")]
    [InlineData("SuperAdmin", "super@example.test")]
    public async Task AdminLoginMeRefreshLogout_PreserveRolesAndRotateRefreshTokens(string role, string email)
    {
        var id = role == "Admin" ? Admin : SuperAdmin;
        using var login = await Send("POST", "/api/v1/admin/auth/login", new { email, password = Password });
        AssertIdentity(await SuccessData(login), id, role);
        await AssertCookiesAndNoTokenBody(login);
        var cookies = ResponseCookies(login);
        using var me = await Send("GET", "/api/v1/admin/auth/me", cookie: cookies);
        AssertIdentity(await SuccessData(me), id, role);
        using var refresh = await Send("POST", "/api/v1/admin/auth/refresh", cookie: RefreshCookie(cookies));
        AssertIdentity(await SuccessData(refresh), id, role);
        var current = ResponseCookies(refresh);
        using var replay = await Send("POST", "/api/v1/admin/auth/refresh", cookie: RefreshCookie(cookies));
        await Error(replay, 401);
        using var logout = await Send("POST", "/api/v1/admin/auth/logout", cookie: current);
        await SuccessData(logout);
        using var revoked = await Send("POST", "/api/v1/admin/auth/refresh", cookie: RefreshCookie(current));
        await Error(revoked, 401);
        // Admin access JWTs remain stateless until expiry; logout revokes refresh and clears cookies.
        using var stateless = await Send("GET", "/api/v1/admin/auth/me", cookie: current);
        AssertIdentity(await SuccessData(stateless), id, role);
    }

    [Theory]
    [InlineData("/api/v1/auth/refresh", "missing")]
    [InlineData("/api/v1/auth/refresh", "unknown")]
    [InlineData("/api/v1/auth/refresh", "expired")]
    [InlineData("/api/v1/auth/refresh", "revoked")]
    [InlineData("/api/v1/admin/auth/refresh", "missing")]
    [InlineData("/api/v1/admin/auth/refresh", "unknown")]
    [InlineData("/api/v1/admin/auth/refresh", "expired")]
    [InlineData("/api/v1/admin/auth/refresh", "revoked")]
    public async Task Refresh_RejectsMissingUnknownExpiredAndRevokedTokens(string route, string state)
    {
        const string token = "invalid-refresh-credential";
        if (state is "expired" or "revoked")
            await WithDb(async db =>
            {
                if (route.Contains("admin")) db.SystemAdminRefreshTokens.Add(new SystemAdminRefreshTokens
                    { AdminId = Admin, Token = token, IsActive = state != "revoked", ExpDate = DateTime.UtcNow.AddDays(state == "expired" ? -1 : 1) });
                else db.UserRefreshTokens.Add(new UserRefreshToken
                    { UserId = UserA, Token = token, IsActive = state != "revoked", ExpDate = DateTime.UtcNow.AddDays(state == "expired" ? -1 : 1) });
                await db.SaveChangesAsync();
            });
        using var response = await Send("POST", route, cookie: state == "missing" ? null : "X-Refresh-Token=" + token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/auth/me")]
    [InlineData("POST", "/api/v1/admin/auth/logout")]
    public async Task AdminSessionEndpoints_RejectRegularUsersAndAnonymous(string method, string route)
    {
        using var anonymous = await Send(method, route);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var regular = await Send(method, route, cookie: AccessCookie(UserA));
        Assert.Equal(HttpStatusCode.Forbidden, regular.StatusCode);
    }

    [Fact]
    public async Task UserLogout_CannotRevokeAnotherUsersRefreshToken()
    {
        await WithDb(async db =>
        {
            db.UserRefreshTokens.Add(new UserRefreshToken { UserId = UserB, Token = "other-refresh-secret", ExpDate = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
        });
        using var response = await Send("POST", "/api/v1/auth/logout", cookie: AccessCookie(UserA) + "; X-Refresh-Token=other-refresh-secret");
        await SuccessData(response);
        await WithDb(async db =>
        {
            Assert.True((await db.UserRefreshTokens.SingleAsync()).IsActive);
            Assert.Equal(SessionB, (await db.Users.FindAsync(UserB))!.CurrentSessionToken);
        });
    }

    [Fact]
    public async Task AdminLogout_CannotRevokeAnotherAdminsRefreshToken()
    {
        await WithDb(async db =>
        {
            db.SystemAdminRefreshTokens.Add(new SystemAdminRefreshTokens { AdminId = SuperAdmin, Token = "other-admin-refresh", ExpDate = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
        });
        using var response = await Send("POST", "/api/v1/admin/auth/logout", cookie: AccessCookie(Admin, "Admin") + "; X-Refresh-Token=other-admin-refresh");
        await SuccessData(response);
        await WithDb(async db => Assert.True((await db.SystemAdminRefreshTokens.SingleAsync()).IsActive));
    }

    [Fact]
    public async Task RefreshAndRejectedSession_DoNotLogAuthenticationCredentials()
    {
        const string refresh = "refresh-value-must-never-be-logged";
        const string stale = "stale-session-must-never-be-logged";
        using var rejectedRefresh = await Send("POST", "/api/v1/auth/refresh", cookie: "X-Refresh-Token=" + refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedRefresh.StatusCode);
        using var rejectedSession = await Send("GET", "/api/v1/auth/me", cookie: AccessCookie(UserA, session: stale));
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedSession.StatusCode);
        var log = string.Join("\n", Logs);
        Assert.DoesNotContain(refresh, log); Assert.DoesNotContain(stale, log); Assert.DoesNotContain(SessionA, log);
    }

    private static string RefreshCookie(string cookies) => cookies.Split("; ").Single(cookie => cookie.StartsWith("X-Refresh-Token="));

    private static void AssertIdentity(System.Text.Json.JsonElement data, Guid id, string role)
    {
        Assert.Equal(new[] { "email", "name", "role", "userId" }, data.EnumerateObject().Select(value => value.Name).OrderBy(value => value));
        Assert.Equal(id.ToString(), data.GetProperty("userId").GetString()); Assert.Equal(role, data.GetProperty("role").GetString());
    }

    private static async Task AssertCookiesAndNoTokenBody(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Equal(2, cookies.Count);
        foreach (var cookie in cookies)
        {
            Assert.Contains("httponly", cookie); Assert.Contains("secure", cookie); Assert.Contains("samesite=none", cookie); Assert.Contains("path=/", cookie);
            Assert.DoesNotContain(cookie.Split(';')[0].Split('=', 2)[1], body);
        }
    }
}
