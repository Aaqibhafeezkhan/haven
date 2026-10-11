using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Messaging;
using Haven.Application.Features.ApiTokens.Abstractions;

namespace Haven.Application.Features.ApiTokens.Queries.GetApiTokensPaged;

public class GetApiTokensPagedHandler(IApiTokenRepository repository, ICurrentUserService currentUserService)
    : IPagedQueryHandler<GetApiTokensPagedQuery, ApiTokenResult>
{
    public async ValueTask<PagedResult<ApiTokenResult>> Handle(GetApiTokensPagedQuery query,
        CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null)
            return PagedResult<ApiTokenResult>.Empty();

        var tokens = await repository.GetPagedByUserIdAsync(currentUserId.Value, query.PageNumber, query.PageSize,
            cancellationToken);

        return tokens.Project(t => new ApiTokenResult()
        {
            Id = t.Id,
            DisplayName = t.DisplayName,
            Prefix = t.Prefix,
            Scopes = t.Scopes.Select(s => s.Scope).ToArray(),
            ExpiresAt = t.ExpiresAt,
            CreatedAt = t.CreatedAt,
            LastUsedAt = t.LastUsedAt,
            RevokedAt = t.RevokedAt
        });
    }
}