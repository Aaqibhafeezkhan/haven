using FastEndpoints;

using Haven.Application.Common.Responses;
using Haven.Application.Features.ApiTokens.Commands;
using Haven.Presentation.Api.Extensions;

using Mediator;

namespace Haven.Presentation.Api.Endpoints.Auth;

public class GenerateApiTokenEndpoint(IMediator mediator) : Endpoint<GenerateApiTokenRequest, ApiResponse<GenerateApiTokenResult>>
{
    public override void Configure()
    {
        Post("auth/api-tokens");
        Options(x => x.WithTags("Auth"));
        Summary(s =>
        {
            s.Summary = "Generate API Token";
            s.Description = "Generates a new API token with the specified name, scopes, and optional expiration.";
            s[200] = "API token generated successfully";
            s[400] = "Validation error";
            s[401] = "Unauthorized";
        });
    }

    public override async Task HandleAsync(GenerateApiTokenRequest req, CancellationToken ct)
    {
        var command = new GenerateApiTokenCommand
        {
            Name = req.Name,
            Scopes = req.Scopes,
            Expiration = req.Expiration
        };

        var result = await mediator.Send(command, ct);

        await this.SendResultAsync(result, ct);
    }
}

public class GenerateApiTokenRequest
{
    public string Name { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public TimeSpan? Expiration { get; set; }
}