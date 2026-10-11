using FastEndpoints;

using Haven.Application.Common.Responses;
using Haven.Application.Features.ApiTokens.Commands;
using Haven.Presentation.Api.Extensions;

using Mediator;

namespace Haven.Presentation.Api.Endpoints.Auth;

public class RegenerateApiTokenEndpoint(IMediator mediator) : EndpointWithoutRequest<ApiResponse<GenerateApiTokenResult>>
{
    public override void Configure()
    {
        Post("auth/api-tokens/{id}/regenerate");
        Options(x => x.WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Regenerate API Token";
            s.Description = "Replaces the secret of an existing API token, invalidating the previous one. Scopes are kept.";
            s[200] = "API token regenerated successfully";
            s[400] = "Token is revoked";
            s[401] = "Unauthorized";
            s[404] = "API token not found";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new RegenerateApiTokenCommand { Id = Route<Guid>("id") }, ct);
        await this.SendResultAsync(result, ct);
    }
}
