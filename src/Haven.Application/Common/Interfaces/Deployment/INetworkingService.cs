using Haven.Domain;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;

namespace Haven.Application.Common.Interfaces.Deployment;

public interface INetworkingService
{
    ServiceType ServiceType { get; }
    Task<Result> CreateProjectEnvironmentNetworkAsync(Guid projectId, Guid environmentId, CancellationToken cancellationToken);
    Task<Result> ConnectServiceToNetworksAsync(Guid serviceId, IEnumerable<Guid> networkIds, CancellationToken cancellationToken);
    Task<Result> DisconnectServiceFromNetworksAsync(Guid serviceId, IEnumerable<Guid> networkIds, CancellationToken cancellationToken);
    Task<Result> DisconnectServiceFromAllNetworksAsync(Guid serviceId, CancellationToken cancellationToken);
    Task<Result> EnsureNetworkExistsAsync(Guid networkId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the Docker network and provisions a fresh one under the same name on a subnet from the
    /// configured pool. Refuses with <c>NetworkInUse</c> while any container is still attached.
    /// </summary>
    Task<Result> RecreateNetworkAsync(Guid networkId, string reason, CancellationToken cancellationToken);

    /// <summary>True when any container (running or stopped) is attached to the network's Docker network.</summary>
    Task<bool> IsNetworkInUseAsync(Guid networkId, CancellationToken cancellationToken);

    Task<Result> DeleteNetworkAsync(Guid networkId, CancellationToken cancellationToken);
}