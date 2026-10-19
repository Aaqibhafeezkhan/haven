using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.ServiceRegistry.Queries.GetServiceRegistrySummary;

public sealed class GetServiceRegistrySummaryHandler(IServiceRegistryEntryRepository repository)
    : IQueryHandler<GetServiceRegistrySummaryQuery, ServiceRegistrySummaryDto>
{
    public async ValueTask<Result<ServiceRegistrySummaryDto>> Handle(GetServiceRegistrySummaryQuery query, CancellationToken cancellationToken)
    {
        var s = await repository.GetSummaryAsync(cancellationToken);
        return Result<ServiceRegistrySummaryDto>.Success(new ServiceRegistrySummaryDto
        {
            Total = s.Total,
            Running = s.Running,
            Stopped = s.Stopped,
            Starting = s.Starting,
            Unknown = s.Unknown,
            DomainCount = s.DomainCount,
            ExposedExternally = s.ExposedExternally,
        });
    }
}