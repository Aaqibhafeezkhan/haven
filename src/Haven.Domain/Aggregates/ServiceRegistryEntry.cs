using System.Text.Json.Serialization;

using Haven.Domain.Entities;
using Haven.Domain.Enums;
using Haven.Domain.Events;
using Haven.Domain.Exceptions;
using Haven.Domain.ValueObjects;

namespace Haven.Domain.Aggregates;

public class ServiceRegistryEntry : AggregateRoot
{
    /// <summary>
    /// Exactly one of <see cref="ServiceId"/>/<see cref="SidecarId"/> is set - which one an entry
    /// is "for" determines which aggregate owns its <see cref="Domains"/>. Enforced by the
    /// <see cref="Create"/>/<see cref="CreateForSidecar"/> factories and mirrored by a DB check
    /// constraint.
    /// </summary>
    public Guid? ServiceId { get; set; }
    public Guid? SidecarId { get; set; }
    public string? ContainerName { get; set; }
    public string? IpAddress { get; set; }
    public List<PortMapping> Ports { get; set; } = [];
    public ServiceRuntimeState RuntimeState { get; private set; } = ServiceRuntimeState.NotDeployed;
    public ServiceHealth Health { get; private set; } = ServiceHealth.Unknown;

    /// <summary>When <see cref="RuntimeState"/> last changed. Starts at registration.</summary>
    public DateTime StateChangedAt { get; private set; }

    /// <summary>Derived from which owner id is set; not persisted.</summary>
    [JsonIgnore] public OwnerKind OwnerKind => ServiceId.HasValue ? OwnerKind.Service : OwnerKind.Sidecar;
    [JsonIgnore] public Guid OwnerId => ServiceId ?? SidecarId ?? Guid.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }

    public ICollection<ServiceRegistryDomain> Domains { get; set; } = [];

    [JsonIgnore] public Service? Service { get; set; }
    [JsonIgnore] public Sidecar? Sidecar { get; set; }

    public static ServiceRegistryEntry Create(Guid serviceId)
    {
        var now = DateTime.UtcNow;
        return new ServiceRegistryEntry
        {
            Id = Guid.NewGuid(),
            ServiceId = serviceId,
            RegisteredAt = now,
            UpdatedAt = now,
            StateChangedAt = now
        };
    }

    public static ServiceRegistryEntry CreateForSidecar(Guid sidecarId)
    {
        var now = DateTime.UtcNow;
        return new ServiceRegistryEntry
        {
            Id = Guid.NewGuid(),
            SidecarId = sidecarId,
            RegisteredAt = now,
            UpdatedAt = now,
            StateChangedAt = now
        };
    }

    public void UpdateNetwork(string ip, List<PortMapping> ports)
    {
        IpAddress = ip;
        Ports = ports;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies an observed runtime state. Leaving <see cref="ServiceRuntimeState.Running"/> resets
    /// <see cref="Health"/> to Unknown, since health is only meaningful while running.
    /// </summary>
    /// <returns><c>true</c> if the state changed.</returns>
    public bool ApplyRuntimeState(ServiceRuntimeState next, DateTime now)
    {
        if (RuntimeState == next) return false;

        var previous = RuntimeState;
        RuntimeState = next;
        StateChangedAt = now;
        UpdatedAt = now;
        StartedAt = next == ServiceRuntimeState.Running ? now : null;

        Raise(new ServiceRuntimeStateChangedEvent(Id, OwnerKind, OwnerId, previous, next));

        if (previous == ServiceRuntimeState.Running && Health != ServiceHealth.Unknown)
        {
            var previousHealth = Health;
            Health = ServiceHealth.Unknown;
            Raise(new ServiceHealthChangedEvent(Id, OwnerKind, OwnerId, previousHealth, ServiceHealth.Unknown));
        }

        return true;
    }

    /// <summary>
    /// Applies an observed health result. Ignored unless the entry is running.
    /// </summary>
    /// <returns><c>true</c> if the health changed.</returns>
    public bool ApplyHealth(ServiceHealth next)
    {
        if (RuntimeState != ServiceRuntimeState.Running) return false;
        if (Health == next) return false;

        var previous = Health;
        Health = next;
        UpdatedAt = DateTime.UtcNow;
        Raise(new ServiceHealthChangedEvent(Id, OwnerKind, OwnerId, previous, next));
        return true;
    }

    public ServiceRegistryDomain AddDomain(string hostname, int containerPort, TlsMode tlsMode = TlsMode.None, string? internalBasePath = null)
    {
        EnsureHostnameNotTakenLocally(hostname, excludingDomainId: null);

        var domain = ServiceRegistryDomain.Create(Id, hostname, containerPort, tlsMode, internalBasePath);
        Domains.Add(domain);
        UpdatedAt = DateTime.UtcNow;
        return domain;
    }

    public void UpdateDomain(ServiceRegistryDomain domain, Optional<string> hostname, Optional<int> containerPort, Optional<TlsMode> tlsMode = default, Optional<string> internalBasePath = default)
    {
        if (!Domains.Contains(domain))
            throw new ValidationException("The domain does not belong to this service registry entry.");

        if (hostname.HasValue)
            EnsureHostnameNotTakenLocally(hostname.Value, excludingDomainId: domain.Id);

        domain.Apply(hostname, containerPort, tlsMode, internalBasePath);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveDomain(ServiceRegistryDomain domain)
    {
        if (Domains.Remove(domain))
            UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Guards only against duplicate hostnames within this entry's own domains. The authoritative,
    /// instance-wide uniqueness check happens in the application layer (AddDomainHandler/UpdateDomainHandler)
    /// via a repository lookup across all registry entries, plus a DB-level unique index as the final guarantee.
    /// </summary>
    private void EnsureHostnameNotTakenLocally(string hostname, Guid? excludingDomainId)
    {
        var normalized = hostname?.Trim().ToLowerInvariant() ?? string.Empty;
        if (Domains.Any(d => d.Id != excludingDomainId && d.Hostname == normalized))
            throw new ValidationException($"A domain with hostname '{normalized}' already exists on this service.");
    }
}