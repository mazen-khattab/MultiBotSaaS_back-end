using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using SaaS.Api.Configuration;
using SaaS.Api.Extensions;

namespace SaaS.Api.IntegrationTests;

public sealed class FrontendCorsConfigurationTests
{
    [Fact]
    public void ShippedDevelopmentSettings_AllowLocalFrontendsWithCredentials()
    {
        var policy = BuildPolicy(ShippedSettings(Environments.Development), Environments.Development);
        Assert.Equal(new[] { "http://localhost:5173", "http://localhost:3000" }, policy.Origins);
        Assert.True(policy.SupportsCredentials);
        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.AllowAnyMethod);
        Assert.False(policy.AllowAnyOrigin);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void ShippedNonDevelopmentSettings_RequireExplicitFrontendConfiguration(string environment)
    {
        var error = Assert.Throws<OptionsValidationException>(() => BuildPolicy(ShippedSettings(environment), environment));
        Assert.Contains("Cors:AllowedOrigins", error.Message);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void EnvironmentVariables_OverrideSettingsWithoutInheritingDevelopmentOrigins(string environment)
    {
        // A unique prefix keeps parallel test runs isolated from process-wide environment state.
        var prefix = "MULTIBOT_CORS_TEST_" + Guid.NewGuid().ToString("N") + "_";
        var key = prefix + "Cors__AllowedOrigins__0";
        var origin = $"https://{environment.ToLowerInvariant()}.example.test";
        Environment.SetEnvironmentVariable(key, origin);
        try
        {
            var configuration = ShippedSettings(environment).AddEnvironmentVariables(prefix).Build();
            Assert.Equal(origin, Assert.Single(BuildPolicy(configuration, environment).Origins));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void Origins_AreNormalizedAndDeduplicated()
    {
        var policy = BuildPolicy(Origins(" HTTPS://APP.EXAMPLE.TEST:443/ ", "https://app.example.test", "https://admin.example.test:8443/"));
        Assert.Equal(new[] { "https://app.example.test", "https://admin.example.test:8443" }, policy.Origins);
    }

    [Theory]
    [InlineData("http://localhost:5173/", "http://localhost:5173")]
    [InlineData("https://[::1]:7062/", "https://[::1]:7062")]
    [InlineData("https://bücher.example/", "https://xn--bcher-kva.example")]
    public void ValidOriginForms_AreCanonicalized(string configured, string expected)
    {
        Assert.Equal(expected, Assert.Single(BuildPolicy(Origins(configured), Environments.Development).Origins));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("*")]
    [InlineData("https://*.example.test")]
    [InlineData("null")]
    [InlineData("example.test")]
    [InlineData("//example.test")]
    [InlineData("ftp://example.test")]
    [InlineData("http://localhost:5173")]
    [InlineData("https://user:credential-secret@example.test")]
    [InlineData("https://@example.test")]
    [InlineData("https://example.test/frontend")]
    [InlineData("https://example.test/path/..")]
    [InlineData("https://example.test//")]
    [InlineData("https://example.test?query=1")]
    [InlineData("https://example.test?")]
    [InlineData("https://example.test#fragment")]
    [InlineData("https://example.test#")]
    [InlineData("https://example.test:99999")]
    [InlineData("https://exa mple.test")]
    [InlineData("https://example.test,https://other.test")]
    [InlineData("https:\\example.test")]
    public void InvalidOrigins_FailStartupValidationWithoutEchoingInput(string? origin)
    {
        var error = Assert.Throws<OptionsValidationException>(() => BuildPolicy(Origins(origin)));
        Assert.Contains("Cors:AllowedOrigins", error.Message);
        Assert.DoesNotContain("credential-secret", error.Message);
    }

    [Fact]
    public void MissingOrigins_AreRejectedInsteadOfAllowingEveryOrigin()
    {
        Assert.Throws<OptionsValidationException>(() => BuildPolicy(new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void OneInvalidEntry_InvalidatesTheWholeConfiguration()
    {
        Assert.Throws<OptionsValidationException>(() => BuildPolicy(Origins("https://app.example.test", "*")));
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Preview")]
    public void HttpOrigins_AreLimitedToDevelopment(string environment)
    {
        Assert.Throws<OptionsValidationException>(() => BuildPolicy(Origins("http://localhost:5173"), environment));
    }

    private static IConfigurationBuilder ShippedSettings(string environment) => new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{environment}.json", optional: true);

    private static IConfiguration Origins(params string?[] origins) => new ConfigurationBuilder()
        .AddInMemoryCollection(origins.Select((origin, index) =>
            new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin))).Build();

    private static CorsPolicy BuildPolicy(IConfigurationBuilder configuration, string environment) => BuildPolicy(configuration.Build(), environment);

    private static CorsPolicy BuildPolicy(IConfiguration configuration, string environment = "Production")
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(value => value.EnvironmentName).Returns(environment);
        var services = new ServiceCollection();
        services.AddFrontendCors(configuration, hostEnvironment.Object);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<CorsOptions>>().Value.GetPolicy(FrontendCorsSettings.PolicyName)!;
    }
}
