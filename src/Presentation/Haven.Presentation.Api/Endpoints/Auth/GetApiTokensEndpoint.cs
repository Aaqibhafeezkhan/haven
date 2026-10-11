using FastEndpoints;

using Haven.Application.Common.Messaging;
using Haven.Application.Features.ApiTokens.Queries;
using Haven.Application.Features.ApiTokens.Queries.GetApiTokensPaged;
using Haven.Presentation.Api.Extensions;

using Mediator;

namespace Haven.Presentation.Api.Endpoints.Auth;

public class GetApiTokensEndpoint(IMediator mediator) : Endpoint<GetApiTokensPagedQuery, PagedResult<ApiTokenResult>>
{
    public override void Configure()
    {
        Get("auth/api-tokens");
        Options(x => x.WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Get API Tokens";
            s.Description = "Gets the current user's API tokens, paged.";
            s[200] = "API tokens retrieved successfully";
            s[401] = "Unauthorized";
        });
    }

    public override async Task HandleAsync(GetApiTokensPagedQuery req, CancellationToken ct)
    {
        var result = await mediator.Send(req, ct);
        await this.SendResultAsync(result, ct);
    }
}