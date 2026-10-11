using Hangfire.Logging;

using Haven.Application.Features.ApiTokens.Abstractions;
using Haven.Domain.Entities;
using Haven.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Haven.Infrastructure.Auth;

public class ApiTokenService(HavenDbContext db, ILogger<ApiTokenService> logger) : IApiTokenService
{
    public async Task<(ApiToken Token, string PlainText)> CreateAsync(Guid ownerId, string name, IEnumerable<string> scopes, TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var (plain, hash, prefix) = TokenGenerator.Generate();

        var token = new ApiToken()
        {
            OwnerId = ownerId,
            DisplayName = name,
            TokenHash = hash,
            Prefix = prefix,
            Scopes = scopes.Select(s => new ApiTokenScope { Scope = s }).ToList(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : null
        };
        
        db.ApiTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
        return (token, plain);
    }

    public async Task<ApiToken?> ValidateAsync(string plainText, CancellationToken cancellationToken = default)
    {
        var hash = TokenGenerator.Hash(plainText);
        var token = await db.ApiTokens
            .Include(t => t.Scopes)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        
        if (token is null || !token.IsActive(DateTime.UtcNow))
        {
            logger.LogWarning("Invalid or inactive API token used: {TokenPrefix}", token?.Prefix ?? "unknown");
            return null;
        }
        
        token.MarkUsed(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        
        return token;
    }

    public async Task RevokeAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var token = db.ApiTokens.FirstOrDefault(t => t.Id == tokenId);
        if (token is null)
        {
            logger.LogWarning("Attempted to revoke non-existent API token with ID: {TokenId}", tokenId);
            return;
        }

        token.Revoke(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }
}