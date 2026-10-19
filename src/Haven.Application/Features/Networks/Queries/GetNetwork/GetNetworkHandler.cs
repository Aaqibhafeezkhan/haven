using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;
using Haven.Application.Common.Messaging;
using Haven.Application.Features.Networks.Queries.ListNetworks;
using Haven.Application.Mappers;
using Haven.Domain.Aggregates;

namespace Haven.Application.Features.Networks.Queries.GetNetwork;

public sealed class GetNetworkHandler(INetworkRepository networkRepository, IServiceStateReader stateReader)
    : IQueryHandler<GetNetworkQuery, NetworkDto>
{
    public async ValueTask<Result<NetworkDto>> Handle(GetNetworkQuery query, CancellationToken cancellationToken)
    {
        var network = await networkRepository.GetByIdAsync(query.NetworkId, cancellationToken);
        if (network is null)
            return Error.NotFoundFor(nameof(Network), query.NetworkId);

        var state = await stateReader.LoadAsync(network.ServiceNetworks.Select(sn => sn.ServiceId), cancellationToken);
        return Result<NetworkDto>.Success(network.ToDto(state));
    }
}