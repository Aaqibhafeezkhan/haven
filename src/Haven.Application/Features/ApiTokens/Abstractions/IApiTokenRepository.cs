using Haven.Application.Common.Messaging;
using Haven.Domain.Entities;

namespace Haven.Application.Features.ApiTokens.Abstractions;

public interface IApiTokenRepository
{
    Task<ApiToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<PagedResult<ApiToken>> GetPagedByUserIdAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task CreateAsync(ApiToken token, CancellationToken cancellationToken = default);
}
