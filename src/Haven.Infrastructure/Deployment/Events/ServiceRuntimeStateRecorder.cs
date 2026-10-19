using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;

namespace Haven.Infrastructure.Deployment.Events;

/// <summary>
/// Applies runtime state and health observed from Docker to a service's or sidecar's registry entry.
/// The caller saves changes so domain events dispatch with its own unit of work.
/// </summary>
public sealed class ServiceRuntimeStateRecorder(
    IServiceRegistry registry,
    IDeploymentRepository deploymentRepository)
{
    /// <summary>
    /// Applies a stop-like signal (died, killed, stopped, out of memory). Ignored while a deployment is in
    /// flight or the container is restarting, since those intentionally stop the old container. See
    /// <see cref="ServiceRuntimeStateMapper.ForStopSignal"/> for how the state is chosen.
    /// </summary>
    /// <returns><c>true</c> if the entry changed.</returns>
    public async Task<bool> ApplyStopAsync(IDeployableContainer container, ServiceRuntimeState unexpectedState, int? exitCode, CancellationToken ct)
    {
        var entry = await EnsureEntryAsync(container, ct);

        if (entry.RuntimeState == ServiceRuntimeState.Restarting)
            return false;

        if (container is Service)
        {
            var active = await deploymentRepository.GetActiveStatusesAsync([container.Id], ct);
            if (active.ContainsKey(container.Id))
                return false;
        }

        var next = ServiceRuntimeStateMapper.ForStopSignal(container.DesiredState, unexpectedState, exitCode);
        return entry.ApplyRuntimeState(next, DateTime.UtcNow);
    }

    public async Task<bool> ApplyRunningAsync(IDeployableContainer container, CancellationToken ct)
    {
        var entry = await EnsureEntryAsync(container, ct);
        return entry.ApplyRuntimeState(ServiceRuntimeState.Running, DateTime.UtcNow);
    }

    public async Task<bool> ApplyHealthAsync(IDeployableContainer container, ServiceHealth health, CancellationToken ct)
    {
        var entry = await EnsureEntryAsync(container, ct);
        return entry.ApplyHealth(health);
    }

    private Task<ServiceRegistryEntry> EnsureEntryAsync(IDeployableContainer container, CancellationToken ct) =>
        container is Sidecar
            ? registry.EnsureSidecarRegisteredAsync(container.Id, ct)
            : registry.EnsureServiceRegisteredAsync(container.Id, ct);
}
