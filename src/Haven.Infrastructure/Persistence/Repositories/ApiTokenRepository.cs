using Haven.Application.Common.Messaging;
using Haven.Application.Features.ApiTokens.Abstractions;
using Haven.Domain.Entities;
using Haven.Infrastructure.Persistence.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Haven.Infrastructure.Persistence.Repositories;

public class ApiTokenRepository(HavenDbContext context) : IApiTokenRepository
{
    public Task<ApiToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.ApiTokens
            .Include(t => t.Scopes)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => context.ApiTokens
            .Include(t => t.Scopes)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task<PagedResult<ApiToken>> GetPagedByUserIdAsync(Guid userId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
        => context.ApiTokens
            .Include(t => t.Scopes)
            .Where(t => t.OwnerId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToPagedResultAsync(pageNumber, pageSize, cancellationToken);

    public Task CreateAsync(ApiToken token, CancellationToken cancellationToken = default)
    {
        context.ApiTokens.Add(token);
        return Task.CompletedTask;
    }
}