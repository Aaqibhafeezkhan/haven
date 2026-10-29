using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;

namespace Haven.Application.Common.Services;

public sealed class ServiceStateReader(
    IServiceRegistryEntryRepository registryRepository,
    IDeploymentRepository deploymentRepository) : IServiceStateReader
{
    public async Task<ServiceStateSnapshot> LoadAsync(IEnumerable<Guid> ownerIds, CancellationToken ct = default)
    {
        var ids = ownerIds.Distinct().ToList();
        if (ids.Count == 0) return ServiceStateSnapshot.Empty;

        var entries = await registryRepository.GetByOwnerIdsAsync(ids, ct);
        var active = await deploymentRepository.GetActiveStatusesAsync(ids, ct);
        var failed = await deploymentRepository.GetServicesWithLatestFailedDeploymentAsync(ids, ct);
        return new ServiceStateSnapshot(entries, active, failed);
    }
}