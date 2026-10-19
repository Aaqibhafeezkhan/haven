using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Messaging;
using Haven.Domain.Enums;

namespace Haven.Application.Features.ServiceRegistry.Queries.GetServiceRegistryEntries;

[RequirePermission(Permissions.ProjectManagement.Read)]
public sealed class GetServiceRegistryEntriesQuery : PagedQuery<ServiceRegistryEntryDto>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }

    public RegistryStatusFilter? Status { get; init; }
    public ExposureMode? Exposure { get; init; }
    public ServiceType? Type { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? EnvironmentId { get; init; }

    /// <summary>One of: name, project, type, status, exposure, uptime, updatedAt. Unknown values fall back to updatedAt.</summary>
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}