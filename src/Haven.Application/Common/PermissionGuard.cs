using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Domain.Exceptions;

namespace Haven.Application.Common;

/// <summary>
/// Shared permission check for the current caller. Used by <c>PermissionBehavior</c> for command-level
/// <see cref="RequirePermissionAttribute"/>s and by handlers that enforce data-driven permissions
/// (e.g. a custom action's own required permissions).
/// </summary>
public static class PermissionGuard
{
    /// <summary>
    /// Throws <see cref="ForbiddenException"/> unless the caller holds every permission in <paramref name="required"/>.
    /// An API token acts on behalf of its owner, so both must grant each permission: the token's scopes
    /// (what it was issued for) and the owner (what they currently hold).
    /// </summary>
    public static async Task EnsureAsync(
        ICurrentUserService currentUserService,
        IPermissionRepository permissionRepository,
        IReadOnlyCollection<string> required,
        CancellationToken ct)
    {
        if (required.Count == 0 || currentUserService.IsBackgroundContext)
            return;

        var userId = currentUserService.UserId
                     ?? throw new ForbiddenException();

        if (currentUserService.IsApiToken)
        {
            var scopes = currentUserService.Scopes;
            if (required.Any(permission => !scopes.Contains(permission)))
                throw new ForbiddenException();
        }

        foreach (var permission in required)
        {
            if (!await permissionRepository.UserHasPermissionAsync(userId, permission, ct))
                throw new ForbiddenException();
        }
    }
}
