using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;

namespace Haven.Infrastructure.Deployment.Events;

/// <summary>
/// Applies runtime state and health observed from Docker to a service's registry entry.
/// The caller saves changes so domain events dispatch with its own unit of work.
/// </summary>
public sealed class ServiceRuntimeStateRecorder(
    IServiceRegistry registry,
    IDeploymentRepository deploymentRepository)
{
    /// <summary>
    /// Applies a stop-like signal (died, killed, stopped, out of memory). Ignored while a deployment is in
    /// flight or the service is restarting, since those intentionally stop the old container. When the operator
    /// wants the service stopped the result is <c>Stopped</c> rather than the supplied unexpected state.
    /// </summary>
    /// <returns><c>true</c> if the entry changed.</returns>
    public async Task<bool> ApplyStopAsync(Service service, ServiceRuntimeState unexpectedState, CancellationToken ct)
    {
        var entry = await registry.EnsureServiceRegisteredAsync(service.Id, ct);

        if (entry.RuntimeState == ServiceRuntimeState.Restarting)
            return false;

        var active = await deploymentRepository.GetActiveStatusesAsync([service.Id], ct);
        if (active.ContainsKey(service.Id))
            return false;

        var next = service.DesiredState == DesiredState.Stopped ? ServiceRuntimeState.Stopped : unexpectedState;
        return entry.ApplyRuntimeState(next, DateTime.UtcNow);
    }

    public async Task<bool> ApplyRunningAsync(Guid serviceId, CancellationToken ct)
    {
        var entry = await registry.EnsureServiceRegisteredAsync(serviceId, ct);
        return entry.ApplyRuntimeState(ServiceRuntimeState.Running, DateTime.UtcNow);
    }

    public async Task<bool> ApplyHealthAsync(Guid serviceId, ServiceHealth health, CancellationToken ct)
    {
        var entry = await registry.EnsureServiceRegisteredAsync(serviceId, ct);
        return entry.ApplyHealth(health);
    }
}
