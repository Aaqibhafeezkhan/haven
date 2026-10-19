using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;
using Haven.Application.Common.Messaging;
using Haven.Application.Mappers;

namespace Haven.Application.Features.Networks.Queries.ListNetworks;

public sealed class ListNetworksHandler(INetworkRepository repository, IServiceStateReader stateReader)
    : IQueryHandler<ListNetworksQuery, List<NetworkDto>>
{
    public async ValueTask<Result<List<NetworkDto>>> Handle(ListNetworksQuery query, CancellationToken cancellationToken)
    {
        var networks = await repository.GetAllAsync(query.Type, cancellationToken);
        var state = await stateReader.LoadAsync(networks.SelectMany(n => n.ServiceNetworks).Select(sn => sn.ServiceId), cancellationToken);
        return networks.Select(n => n.ToDto(state)).ToList();
    }
}