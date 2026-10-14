using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.Networks.Commands.ReconcileNetworks;

public sealed class ReconcileNetworksHandler(INetworkHealthService networkHealthService)
    : ICommandHandler<ReconcileNetworksCommand, NetworkRepairReport>
{
    public async ValueTask<Result<NetworkRepairReport>> Handle(ReconcileNetworksCommand request,
        CancellationToken cancellationToken)
        => await networkHealthService.RepairAsync(request.DryRun, cancellationToken);
}