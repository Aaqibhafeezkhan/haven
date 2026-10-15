using Haven.Application.Common.Messaging;
using Haven.Domain.Aggregates;
using Haven.Domain.Entities;
using Haven.Domain.Enums;

namespace Haven.Application.Common.Interfaces.Repositories;

public interface IServiceRegistryEntryRepository : IRepository
{
    Task<ServiceRegistryEntry?> GetForServiceAsync(Guid serviceId, CancellationToken ct = default);
    Task<ServiceRegistryEntry?> GetForSidecarAsync(Guid sidecarId, CancellationToken ct = default);

    /// <summary>Looks up the owning entry by one of its domains' id - domain ids are globally unique, so this needs no owner context.</summary>
    Task<ServiceRegistryEntry?> GetByDomainIdAsync(Guid domainId, CancellationToken ct = default);

    /// <summary>All domains (across every entry) currently attached to a given library SSL certificate.</summary>
    Task<List<ServiceRegistryDomain>> GetDomainsByCertificateIdAsync(Guid certificateId, CancellationToken ct = default);
    Task<PagedResult<ServiceRegistryEntry>> GetPagedAsync(int pageNumber, int pageSize, ServiceRegistryEntryFilter? filter = null, string? sortBy = null, bool sortDescending = false, CancellationToken ct = default);

    /// <summary>Every domain across all entries, with its owning entry/service/project loaded, for the registry's domains overview.</summary>
    Task<PagedResult<ServiceRegistryDomain>> GetDomainsPagedAsync(int pageNumber, int pageSize, string? search, string? sortBy = null, bool sortDescending = false, CancellationToken ct = default);

    Task<ServiceRegistrySummary> GetSummaryAsync(CancellationToken ct = default);
    Task InsertAsync(ServiceRegistryEntry entry, CancellationToken ct = default);
    Task UpdateAsync(ServiceRegistryEntry entry, CancellationToken ct = default);
    Task DeleteAsync(ServiceRegistryEntry entry, CancellationToken ct = default);
    Task<bool> HostnameExistsAsync(string hostname, Guid? excludingDomainId, CancellationToken ct = default);
}

/// <summary>Coarse runtime state used to filter the registry. <c>Starting</c> covers pending and deploying.</summary>
public enum RegistryStatusFilter
{
    Running,
    Stopped,
    Starting,
    Unknown
}

public sealed record ServiceRegistryEntryFilter(
    string? Search = null,
    RegistryStatusFilter? Status = null,
    ExposureMode? Exposure = null,
    ServiceType? Type = null,
    Guid? ProjectId = null,
    Guid? EnvironmentId = null);

public sealed record ServiceRegistrySummary(
    int Total,
    int Running,
    int Stopped,
    int Starting,
    int Unknown,
    int DomainCount,
    int ExposedExternally);
