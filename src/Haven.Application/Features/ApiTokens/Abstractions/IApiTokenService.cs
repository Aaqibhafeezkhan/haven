using Haven.Domain.Entities;

namespace Haven.Application.Features.ApiTokens.Abstractions;

public interface IApiTokenService
{
    Task<(ApiToken Token, string PlainText)> CreateAsync(Guid ownerId, string name, IEnumerable<string> scopes, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task<ApiToken?> ValidateAsync(string plainText, CancellationToken cancellationToken = default);
    /// <summary>
    /// Replaces the token's secret, invalidating the previous one, and returns the new plaintext.
    /// Scopes are kept; an expiring token's lifetime restarts from now.
    /// </summary>
    Task<string> RegenerateAsync(Guid tokenId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid tokenId, CancellationToken cancellationToken = default);
}