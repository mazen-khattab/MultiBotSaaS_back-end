namespace SaaS.Api.Configuration;

public sealed class FrontendCorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "FrontendPolicy";

    public string[] AllowedOrigins { get; set; } = [];
}
