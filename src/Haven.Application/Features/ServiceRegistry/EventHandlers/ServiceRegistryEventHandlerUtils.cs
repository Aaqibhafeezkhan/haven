using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;

namespace Haven.Application.Features.ServiceRegistry.EventHandlers;

public static class ServiceRegistryEventHandlerUtils
{
    /// <summary>
    /// Makes sure a registry entry exists for the service. Runtime state and health are observed
    /// (Docker events, deployments, health checks) and applied by whoever observes them, never derived from the service.
    /// </summary>
    public static async Task EnsureRegistryEntryAsync(Guid serviceId, IServiceRepository repository, IServiceRegistry registry,
        CancellationToken ct)
    {
        var service = await repository.GetByIdAsync(serviceId, ct);
        if (service is null) return;

        await registry.EnsureServiceRegisteredAsync(serviceId, ct);
    }
}