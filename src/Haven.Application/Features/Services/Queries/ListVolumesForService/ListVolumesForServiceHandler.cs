using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Messaging;
using Haven.Application.Mappers;
using Haven.Domain.Aggregates;
using Haven.Domain.Entities;

namespace Haven.Application.Features.Services.Queries.ListVolumesForService;

public sealed class ListVolumesForServiceHandler(IServiceRepository serviceRepository)
    : IQueryHandler<ListVolumesForServiceQuery, IReadOnlyList<ServiceVolumeDto>>
{
    public async ValueTask<Result<IReadOnlyList<ServiceVolumeDto>>> Handle(ListVolumesForServiceQuery query, CancellationToken cancellationToken)
    {
        var service = await serviceRepository.GetByIdAsync(query.ServiceId, cancellationToken);
        if (service is null)
            return Error.NotFoundFor(nameof(Service), query.ServiceId);
        
        
        Func<ServiceVolume, IComparable> keySelector = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "type" => v => v.Type.ToString(),
            "target" => v => v.Target,
            "createdat" => v => v.CreatedAt,
            "updatedat" => v => v.UpdatedAt,
            _ => v => v.Name,
        };

        var ordered = query.SortDescending
            ? service.Volumes.OrderByDescending(keySelector)
            : service.Volumes.OrderBy(keySelector);

        var volumes = ordered.ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();

        return Result<IReadOnlyList<ServiceVolumeDto>>.Success(volumes.ToDtos());
    }
}