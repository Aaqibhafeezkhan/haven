using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.ServiceRegistry.Queries.GetServiceRegistryDomains;

public sealed class GetServiceRegistryDomainsHandler(IServiceRegistryEntryRepository repository)
    : IPagedQueryHandler<GetServiceRegistryDomainsQuery, ServiceRegistryDomainListItemDto>
{
    public async ValueTask<PagedResult<ServiceRegistryDomainListItemDto>> Handle(GetServiceRegistryDomainsQuery query, CancellationToken cancellationToken)
    {
        var paged = await repository.GetDomainsPagedAsync(query.PageNumber, query.PageSize, query.Search, query.SortBy, query.SortDescending, cancellationToken);
        return paged.Project(d =>
        {
            var entry = d.ServiceRegistryEntry;
            var service = entry?.Service;
            return new ServiceRegistryDomainListItemDto
            {
                Id = d.Id,
                Hostname = d.Hostname,
                ContainerPort = d.ContainerPort,
                TlsMode = d.TlsMode,
                HasCertificate = d.Certificate is not null,
                CertificateName = d.Certificate?.Name,
                CreatedAt = d.CreatedAt,
                ContainerName = entry?.ContainerName,
                Status = entry?.Status ?? Haven.Domain.Enums.ServiceStatus.Unknown,
                ServiceId = entry?.ServiceId,
                ServiceName = service?.Name,
                SidecarName = entry?.Sidecar?.Name,
                EnvironmentName = service?.Environment?.Name,
                ProjectName = service?.Environment?.Project?.Name,
            };
        });
    }
}