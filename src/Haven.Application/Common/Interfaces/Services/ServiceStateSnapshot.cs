using Haven.Domain;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;

namespace Haven.Application.Common.Interfaces.Services;

/// <summary>
/// Point-in-time view of the observed state of a set of services. A service with no registry
/// entry has never been deployed, so lookups on missing ids resolve to <c>NotDeployed</c>.
/// </summary>
public sealed class ServiceStateSnapshot(
    IReadOnlyDictionary<Guid, ServiceRegistryEntry> entries,
    IReadOnlyDictionary<Guid, DeploymentStatus> activeDeployments,
    IReadOnlySet<Guid>? servicesWithFailedDeployments = null)
{
    public static ServiceStateSnapshot Empty { get; } = new(
        new Dictionary<Guid, ServiceRegistryEntry>(),
        new Dictionary<Guid, DeploymentStatus>());

    public ServiceRegistryEntry? GetEntry(Guid serviceId) => entries.GetValueOrDefault(serviceId);

    public DeploymentStatus? GetActiveDeployment(Guid serviceId) =>
        activeDeployments.TryGetValue(serviceId, out var status) ? status : null;

    public ServiceHealth GetHealth(Guid serviceId) => GetEntry(serviceId)?.Health ?? ServiceHealth.Unknown;

    public ServiceRuntimeState GetRuntimeState(Guid serviceId) =>
        GetEntry(serviceId)?.RuntimeState ?? ServiceRuntimeState.NotDeployed;

    public bool HasFailedDeployment(Guid serviceId) => servicesWithFailedDeployments?.Contains(serviceId) == true;

    public ServiceDisplayStatus GetDisplayStatus(Guid serviceId) =>
        ServiceDisplayStatusResolver.Resolve(GetActiveDeployment(serviceId), GetEntry(serviceId)?.RuntimeState, GetEntry(serviceId)?.Health);
}