using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;

namespace Haven.Application.Common.Services;

public sealed class ServiceStateReader(
    IServiceRegistryEntryRepository registryRepository,
    IDeploymentRepository deploymentRepository) : IServiceStateReader
{
    public async Task<ServiceStateSnapshot> LoadAsync(IEnumerable<Guid> serviceIds, CancellationToken ct = default)
    {
        var ids = serviceIds.Distinct().ToList();
        if (ids.Count == 0) return ServiceStateSnapshot.Empty;

        var entries = await registryRepository.GetByServiceIdsAsync(ids, ct);
        var active = await deploymentRepository.GetActiveStatusesAsync(ids, ct);
        return new ServiceStateSnapshot(entries, active);
    }
}
