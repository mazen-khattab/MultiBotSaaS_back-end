using Microsoft.Extensions.Options;
using SaaS.Api.Configuration;

namespace SaaS.Api.Extensions;

public static class FrontendCorsServiceCollectionExtensions
{
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = configuration.GetSection(FrontendCorsSettings.SectionName).Get<FrontendCorsSettings>() ?? new();
        var origins = new List<string>();
        var errors = new List<string>();
        var configuredOrigins = settings.AllowedOrigins ?? [];

        if (configuredOrigins.Length == 0)
            errors.Add("Cors:AllowedOrigins must contain at least one explicit frontend origin.");

        for (var index = 0; index < configuredOrigins.Length; index++)
        {
            if (!TryNormalizeOrigin(configuredOrigins[index], environment.IsDevelopment(), out var origin))
            {
                errors.Add($"Cors:AllowedOrigins:{index} must be an absolute HTTP(S) origin with no wildcard, credentials, path, query or fragment. HTTPS is required outside Development.");
                continue;
            }
            origins.Add(origin);
        }

        // Validate before registering middleware or starting any application services.
        // Error messages identify the setting, without echoing potentially unsafe input.
        if (errors.Count > 0)
            throw new OptionsValidationException(Options.DefaultName, typeof(FrontendCorsSettings), errors);

        var allowedOrigins = origins.Distinct(StringComparer.Ordinal).ToArray();
        services.AddCors(options => options.AddPolicy(FrontendCorsSettings.PolicyName, policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        return services;
    }

    private static bool TryNormalizeOrigin(string? value, bool isDevelopment, out string origin)
    {
        origin = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var candidate = value.Trim();
        if (candidate.Any(char.IsWhiteSpace) || candidate.Contains('*') || candidate.Contains('\\') || candidate.Contains('@') ||
            !Uri.IsWellFormedUriString(candidate, UriKind.Absolute) || !Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            uri.AbsolutePath != "/" || (!isDevelopment && uri.Scheme != "https"))
            return false;

        // Check the original text too: URI parsing can resolve /path/.. back to /.
        var authorityStart = candidate.IndexOf("://", StringComparison.Ordinal) + 3;
        var pathStart = candidate.IndexOf('/', authorityStart);
        if (pathStart >= 0 && pathStart != candidate.Length - 1)
            return false;

        try
        {
            var canonical = new UriBuilder(uri) { Host = uri.IdnHost };
            origin = canonical.Uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped).ToLowerInvariant();
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}
