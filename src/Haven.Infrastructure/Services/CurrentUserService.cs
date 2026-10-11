using System.Security.Claims;

using Haven.Application.Common.Interfaces;
using Haven.Infrastructure.Auth;

using Microsoft.AspNetCore.Http;

namespace Haven.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public bool IsApiToken =>
        httpContextAccessor.HttpContext?.User.HasClaim(
            ApiKeyAuthenticationDefaults.AuthMethodClaimType,
            ApiKeyAuthenticationDefaults.AuthMethodValue) ?? false;

    public IReadOnlyCollection<string> Scopes =>
        httpContextAccessor.HttpContext?.User
            .FindAll(ApiKeyAuthenticationDefaults.ScopeClaimType)
            .Select(c => c.Value)
            .ToArray() ?? [];

    public bool IsAdmin
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user != null &&
                   (user.IsInRole("Admin") || user.HasClaim(c => c.Type == "role" && c.Value == "Admin"));
        }
    }

    public bool IsBackgroundContext => httpContextAccessor.HttpContext is null;
}