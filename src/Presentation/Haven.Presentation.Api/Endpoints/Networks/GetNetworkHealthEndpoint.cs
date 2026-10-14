using FastEndpoints;

using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Features.Networks.Queries.GetNetworkHealth;
using Haven.Presentation.Api.Extensions;

using Mediator;

namespace Haven.Presentation.Api.Endpoints.Networks;

public sealed class GetNetworkHealthEndpoint(IMediator mediator)
    : EndpointWithoutRequest<NetworkHealthReport>
{
    public override void Configure()
    {
        Get("/networks/health");
        Options(x => x.WithTags("Networks"));
        Summary(s =>
        {
            s.Summary = "Network health";
            s.Description = "Compares Haven's networks with Docker and reports drift: stale or missing Docker networks, subnets outside the configured pool, and orphans.";
            s[200] = "OK";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new GetNetworkHealthQuery(), ct);
        await this.SendResultAsync(result, ct);
    }
}
