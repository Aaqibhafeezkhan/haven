using FastEndpoints;

using Haven.Application.Features.ApiTokens.Commands;
using Haven.Presentation.Api.Extensions;

using Mediator;

namespace Haven.Presentation.Api.Endpoints.Auth;

public class RevokeApiTokenEndpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("auth/api-tokens/{id}/revoke");
        Options(x => x.WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Revoke API Token";
            s.Description = "Revokes one of the current user's API tokens. Revoked tokens stop working immediately and cannot be regenerated.";
            s[200] = "API token revoked successfully";
            s[401] = "Unauthorized";
            s[404] = "API token not found";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new RevokeApiTokenCommand { Id = Route<Guid>("id") }, ct);
        await this.SendResultAsync(result, ct);
    }
}