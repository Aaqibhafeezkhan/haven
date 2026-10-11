using System.Security.Claims;
using System.Text.Encodings.Web;

using Haven.Application.Features.ApiTokens.Abstractions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Haven.Infrastructure.Auth;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string ScopeClaimType = "scope";
    public const string AuthMethodClaimType = "auth_method";
    public const string AuthMethodValue = "api_key";
}

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IApiTokenService apiTokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var plainText = Request.Headers[ApiKeyAuthenticationDefaults.HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(plainText))
            return AuthenticateResult.NoResult();

        var token = await apiTokenService.ValidateAsync(plainText, Context.RequestAborted);
        if (token is null)
            return AuthenticateResult.Fail("Invalid API token.");

        var claims = new List<Claim>
        {
            new("sub", token.OwnerId.ToString()),
            new(ApiKeyAuthenticationDefaults.AuthMethodClaimType, ApiKeyAuthenticationDefaults.AuthMethodValue)
        };
        claims.AddRange(token.Scopes.Select(s => new Claim(ApiKeyAuthenticationDefaults.ScopeClaimType, s.Scope)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
