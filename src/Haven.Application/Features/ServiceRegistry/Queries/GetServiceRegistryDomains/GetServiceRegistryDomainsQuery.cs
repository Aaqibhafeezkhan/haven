using Haven.Application.Common;
using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.ServiceRegistry.Queries.GetServiceRegistryDomains;

[RequirePermission(Permissions.ProjectManagement.Read)]
public sealed class GetServiceRegistryDomainsQuery : PagedQuery<ServiceRegistryDomainListItemDto>
{
    public string? Search { get; init; }

    /// <summary>One of: hostname, service, tls, created. Unknown values fall back to hostname.</summary>
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}
