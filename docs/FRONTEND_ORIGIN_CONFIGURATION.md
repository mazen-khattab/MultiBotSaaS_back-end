# Frontend origins and authentication cookies

The API reads browser origins from the `Cors:AllowedOrigins` array at startup. The existing named `FrontendPolicy` allows credentials, all request headers, and all methods for those exact origins. It never permits a wildcard origin.

## Configuration

`SaaS.Api/appsettings.json` contains an empty array. `appsettings.Development.json` supplies the local frontend origins:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "http://localhost:3000"
    ]
  }
}
```

ASP.NET Core loads `appsettings.{Environment}.json` for the selected environment, and environment variables override JSON settings. An environment-specific JSON file can use the same array structure. Staging and Production do not inherit Development origins.

Environment variables use indexed keys: `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, and so on. For example, configure these in the API process's deployment environment:

```powershell
$env:DOTNET_ENVIRONMENT = "Production"
$env:Cors__AllowedOrigins__0 = "https://app.example.com"
$env:Cors__AllowedOrigins__1 = "https://admin.example.com"
```

The domains above illustrate the value format; substitute the deployed frontend origins. For Staging, select `Staging` and configure its own origin, such as `https://staging.example.com`. Use consistent environment settings if the host also sets `ASPNETCORE_ENVIRONMENT`. Origins are captured at startup, so restart/redeploy the API after changing them.

Supply one origin per array entry rather than a comma-separated string. Configuration arrays merge by index across providers: overriding only index `0` in Development retains its existing index `1` (`http://localhost:3000`). The shared settings contain no origins, so Staging/Production use only their explicitly configured entries.

## Validation and normalization

Startup fails before application services run when the origin list is missing, empty, or contains an invalid entry. Errors identify `Cors:AllowedOrigins` and, where applicable, the entry index without echoing its value.

- An origin must be an absolute HTTP(S) scheme, host, and optional port. HTTPS is required outside Development; Development also accepts HTTP.
- Wildcards, embedded credentials, paths, query strings, fragments, and malformed origins are rejected. An optional single root slash is accepted and removed.
- Outer whitespace, scheme/host casing, default ports, and internationalized hostnames are normalized. Duplicate normalized origins are removed.
- Matching uses the complete origin: scheme, host, and non-default port. Allowing `https://app.example.com` does not allow another subdomain or port.

The API keeps `AllowCredentials()`, `AllowAnyHeader()`, and `AllowAnyMethod()`. The CORS middleware runs after routing and before authentication/authorization, so allowed preflights and authentication errors receive the appropriate CORS headers.

## Cookies and deployment topology

Authentication cookies (`X-Access-Token` and `X-Refresh-Token`) retain `HttpOnly=true`, `Secure=true`, and `SameSite=None`. Their path is explicitly `/`, including cookie deletion, and their domain remains unset (host-only on the API host). Token lifetimes and authentication behavior are unchanged.

The frontend must use `withCredentials: true` (or Fetch `credentials: "include"`) and call the HTTPS API URL directly. Do not depend on redirecting an HTTP preflight to HTTPS. `SameSite=None` requires `Secure`.

For local cookie authentication, use the existing HTTPS launch profile and a trusted development certificate:

```powershell
dotnet run --project SaaS.Api --launch-profile https
```

That profile exposes `https://localhost:7062` and `http://localhost:5037`; point the frontend at the HTTPS URL. The Vite frontend can remain at the configured Development origin `http://localhost:5173`.

In production, deploy both frontend and API over HTTPS. For example, `https://app.example.com` can call `https://api.example.com` with the host-only cookies sent to the API. Deployments on unrelated sites also depend on the browser's third-party-cookie policy; correct CORS headers cannot override cookie blocking.

## Browser CORS and server authentication

CORS controls browser access to responses. An unlisted origin receives no CORS permission headers; the underlying endpoint can still respond. CORS does not replace endpoint authorization.

Worker API keys and n8n webhook secrets remain independently enforced by their existing authentication filters. Server-to-server workers/webhooks do not require an allowed browser origin. Do not add webhook URLs or paths to this frontend list.

No secrets are required by the CORS settings. Continue providing existing signing/encryption keys, connection strings, and worker/webhook secrets through local User Secrets or the deployment's environment/secret store, rather than committing secret values.

## Verification

Configuration tests cover shipped Development defaults, explicit Staging/Production settings, environment-variable overrides, normalization, and startup rejection of unsafe values. TestServer integration tests cover credentialed preflights, allowed/unlisted origins, cookie JWT authentication, and independent worker/webhook authentication. Token-service tests verify cookie flags, host-only scope, and deletion path.

References: [ASP.NET Core configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0), [CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0), and [SameSite cookies](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-10.0).
