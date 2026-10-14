using FastEndpoints;

using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Features.Networks.Commands.ReconcileNetworks;
using Haven.Presentation.Api.Extensions;

using Mediator;

namespace Haven.Presentation.Api.Endpoints.Networks;

public sealed class ReconcileNetworksEndpoint(IMediator mediator)
    : Endpoint<ReconcileNetworksCommand, NetworkRepairReport>
{
    public override void Configure()
    {
        Post("/networks/reconcile");
        Options(x => x.WithTags("Networks"));
        Summary(s =>
        {
            s.Summary = "Repair network drift";
            s.Description = "Repairs drifted networks. Networks with containers attached are never touched. Dry run by default; pass dryRun=false to apply.";
            s[200] = "Repair report";
        });
    }

    public override async Task HandleAsync(ReconcileNetworksCommand req, CancellationToken ct)
    {
        var result = await mediator.Send(req, ct);
        await this.SendResultAsync(result, ct);
    }
}
