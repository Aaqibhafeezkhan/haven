using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.Networks.Queries.GetNetworkHealth;

public sealed class GetNetworkHealthHandler(INetworkHealthService networkHealthService)
    : IQueryHandler<GetNetworkHealthQuery, NetworkHealthReport>
{
    public async ValueTask<Result<NetworkHealthReport>> Handle(GetNetworkHealthQuery query,
        CancellationToken cancellationToken)
        => await networkHealthService.AssessAsync(cancellationToken);
}
