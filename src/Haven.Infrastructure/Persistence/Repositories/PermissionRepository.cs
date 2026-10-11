using Haven.Application.Common.Interfaces.Repositories;
using Haven.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Haven.Infrastructure.Persistence.Repositories;

public class PermissionRepository(HavenDbContext context) : IPermissionRepository
{
    public Task<bool> IsUserAdminAsync(Guid userId, CancellationToken cancellationToken)
        => context.Users.AnyAsync(u => u.Id == userId && u.IsAdmin, cancellationToken);

    public async Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken)
    {
        var isAdmin = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IsAdmin)
            .FirstOrDefaultAsync(cancellationToken);

        if (isAdmin)
            return true;

        return await context.Set<UserPermission>()
            .AnyAsync(p => p.UserId == userId && p.Name == permission, cancellationToken);
    }

    public async Task<IEnumerable<UserPermission>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.UserPermissions
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
    }
}