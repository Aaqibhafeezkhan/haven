using Haven.Domain.Entities;

namespace Haven.Application.Features.ApiTokens.Abstractions;

public interface IApiTokenService
{
    Task<(ApiToken Token, string PlainText)> CreateAsync(Guid ownerId, string name, IEnumerable<string> scopes, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task<ApiToken?> ValidateAsync(string plainText, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid tokenId, CancellationToken cancellationToken = default);
}