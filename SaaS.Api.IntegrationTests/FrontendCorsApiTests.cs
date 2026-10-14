using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SaaS.Api.Configuration;
using SaaS.Api.Extensions;
using SaaS.Api.Filters;
using SaaS.Application.Common.Settings;
using SaaS.Infrastructure.Extensions;
using SaaS.Infrastructure.Services;

namespace SaaS.Api.IntegrationTests;

public sealed class FrontendCorsApiTests : IAsyncLifetime
{
    private const string Origin = "https://app.example.test";
    private const string WorkerKey = "cors-test-worker-key";
    private const string WebhookKey = "cors-test-webhook-key";
    private readonly SecuritySettings _security = new()
    {
        JwtSecret = "cors-integration-signing-key-at-least-32-bytes",
        JwtIssuer = "cors-tests", JwtAudience = "cors-tests"
    };
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder().ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHost(web => web.UseTestServer().UseEnvironment(Environments.Production)
                .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:AllowedOrigins:0"] = Origin,
                    ["Cors:AllowedOrigins:1"] = "https://admin.example.test",
                    ["N8nWebhooks:EXAMPLE-BOT"] = "https://worker.example.test/webhook/path"
                }))
                .ConfigureServices((context, services) =>
                {
                    services.AddFrontendCors(context.Configuration, context.HostingEnvironment);
                    services.AddControllers().AddApplicationPart(typeof(CorsProbeController).Assembly);
                    services.AddJwtAuthentication(_security);
                    services.AddAuthorization();
                    services.Configure<WorkerOptions>(options => options.WorkerSecret = WorkerKey);
                    services.Configure<N8nOptions>(options => options.N8nSecret = WebhookKey);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseCors(FrontendCorsSettings.PolicyName);
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                })).StartAsync();
        _client = _host.GetTestClient();
        _client.BaseAddress = new Uri("https://api.example.test");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose(); await _host.StopAsync(); _host.Dispose();
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task AllowedPreflight_PreservesMethodsHeadersAndCredentials(string method)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/cors-probe/public");
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-ui-correlation");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertCorsAllowed(response, Origin);
        Assert.Contains(method, response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        var headers = response.Headers.GetValues("Access-Control-Allow-Headers").Single();
        Assert.Contains("content-type", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("x-ui-correlation", headers, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://app.example.test")]
    [InlineData("https://admin.example.test")]
    public async Task AllowedActualRequest_EchoesExactOriginWithCredentials(string origin)
    {
        using var response = await Send(HttpMethod.Get, "/cors-probe/public", origin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCorsAllowed(response, origin);
        Assert.Contains("Origin", response.Headers.Vary);
    }

    [Theory]
    [InlineData("https://attacker.example.test")]
    [InlineData("https://app.example.test.attacker.invalid")]
    [InlineData("https://app.example.test:8443")]
    [InlineData("http://app.example.test")]
    [InlineData("https://worker.example.test")]
    [InlineData("null")]
    public async Task UnlistedOrigins_ReceiveNoBrowserAccessHeaders(string origin)
    {
        using var response = await Send(HttpMethod.Get, "/cors-probe/public", origin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task UnlistedPreflight_DoesNotReceiveCorsPermission()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/cors-probe/private");
        request.Headers.Add("Origin", "https://attacker.example.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await _client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task AllowedPreflight_IsHandledBeforeEndpointAuthentication()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/cors-probe/private");
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertCorsAllowed(response, Origin);
        using var unauthenticated = await Send(HttpMethod.Get, "/cors-probe/private", Origin);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        AssertCorsAllowed(unauthenticated, Origin);
    }

    [Fact]
    public async Task CookieAuthenticatedRequest_StillUsesExistingJwtCookie()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/cors-probe/private");
        request.Headers.Add("Origin", Origin);
        var token = new JwtSecurityToken(_security.JwtIssuer, _security.JwtAudience,
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "User")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_security.JwtSecret)), SecurityAlgorithms.HmacSha256));
        request.Headers.Add("Cookie", $"{TokenService.AccessTokenCookieName}={new JwtSecurityTokenHandler().WriteToken(token)}");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCorsAllowed(response, Origin);
    }

    [Theory]
    [InlineData("worker", "x-worker-api-key", WorkerKey)]
    [InlineData("webhook", "X-Webhook-Secret", WebhookKey)]
    public async Task WorkerAndWebhookAuthentication_IsIndependentOfBrowserOrigins(string path, string header, string key)
    {
        using var noOrigin = new HttpRequestMessage(HttpMethod.Post, "/cors-probe/" + path);
        noOrigin.Headers.Add(header, key);
        using var success = await _client.SendAsync(noOrigin);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.False(success.Headers.Contains("Access-Control-Allow-Origin"));

        using var missingKey = await Send(HttpMethod.Post, "/cors-probe/" + path, Origin);
        Assert.Equal(HttpStatusCode.Unauthorized, missingKey.StatusCode);
        AssertCorsAllowed(missingKey, Origin);

        using var wrongKey = new HttpRequestMessage(HttpMethod.Post, "/cors-probe/" + path);
        wrongKey.Headers.Add(header, "incorrect-key");
        using var forbidden = await _client.SendAsync(wrongKey);
        Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string origin)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", origin);
        return await _client.SendAsync(request);
    }

    private static void AssertCorsAllowed(HttpResponseMessage response, string origin)
    {
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }
}

// Only this test assembly is registered for these probes; the production API does not load it.
[ApiController]
[Route("cors-probe")]
public sealed class CorsProbeController : ControllerBase
{
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("public")]
    public IActionResult Public() => Ok();

    [HttpGet("private")]
    [Authorize]
    public IActionResult Private() => Ok();

    [HttpPost("worker")]
    [BotWorkerAuthorize]
    public IActionResult Worker() => Ok();

    [HttpPost("webhook")]
    [N8nWebhookAuthorize]
    public IActionResult Webhook() => Ok();
}
