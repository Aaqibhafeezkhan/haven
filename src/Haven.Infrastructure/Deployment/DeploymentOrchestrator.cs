using System.Diagnostics;

using Haven.Application.Common;
using Haven.Application.Common.Contracts;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Services;
using Haven.Application.Common.Telemetry;
using Haven.Domain;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace Haven.Infrastructure.Deployment;

public class DeploymentOrchestrator(
    IUnitOfWork unitOfWork,
    IServiceRegistry registry,
    IDeployServiceFactory deployServiceFactory,
    IDeploymentLogService logService,
    HavenMetrics metrics,
    ILogger<DeploymentOrchestrator> logger) : IDeploymentOrchestrator
{
    public async Task<Result> DeployAsync(IDeployableContainer container, CancellationToken cancellationToken)
    {
        if (container is null) return Error.NotFound;
        if (container is Service serviceContainer && serviceContainer.Environment?.Project is null)
            return Error.NotFound;

        var deployService = deployServiceFactory.Create(container);
        if (deployService is null)
            return Error.NotSupported;

        var tags = Tags(container);

        if (container is Service deployingService)
            deployingService.AnnounceDeploymentStarted();
        else if (container is Sidecar deployingSidecar)
            deployingSidecar.MarkDeploying();

        // Deployment-log persistence only exists for Service today (Deployment.ServiceId is a
        // required FK); sidecars aren't logged there yet, so deploymentId stays null for them.
        Domain.Entities.Deployment? deployment = container is Service loggedService
            ? await logService.CreateDeploymentForServiceAsync(loggedService.Id, cancellationToken)
            : null;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        metrics.DeploymentsStarted.Add(1, tags);

        var sw = Stopwatch.StartNew();

        Result<DeployData> deployResult;
        try
        {
            deployResult = await deployService.DeployAsync(container, deployment?.Id, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            await MarkStoppedAfterFailureAsync(container, CancellationToken.None);
            if (deployment is not null)
                await logService.MarkDeploymentCancelledAsync(deployment.Id, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            metrics.DeploymentsCancelled.Add(1, tags);
            metrics.DeploymentDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "cancelled"));
            return Error.CancelledOperation;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Unhandled exception while deploying '{Name}' ({Id})", container.Name, container.Id);
            await MarkStoppedAfterFailureAsync(container, CancellationToken.None);
            if (deployment is not null)
                await logService.MarkDeploymentFailedAsync(deployment.Id, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            metrics.DeploymentsFailed.Add(1, tags);
            metrics.DeploymentDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "failure"));
            return Error.Failed;
        }

        sw.Stop();

        if (deployResult.IsFailure)
        {
            await MarkStoppedAfterFailureAsync(container, CancellationToken.None);
            if (deployment is not null)
                await logService.MarkDeploymentFailedAsync(deployment.Id, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            metrics.DeploymentsFailed.Add(1, tags);
            metrics.DeploymentDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "failure"));
            return deployResult;
        }

        if (!await TryMarkDeployedAsync(container, cancellationToken))
        {
            if (deployment is not null)
                await logService.MarkDeploymentFailedAsync(deployment.Id, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            metrics.DeploymentsFailed.Add(1, tags);
            metrics.DeploymentDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "failure"));
            return Error.Docker.ContainerCrashedAfterStart;
        }

        if (deployment is not null)
            await logService.MarkDeploymentCompletedAsync(deployment.Id, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        metrics.DeploymentsSucceeded.Add(1, tags);
        metrics.DeploymentDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "success"));

        await PersistRegistryUpdateAsync(
            container,
            deployResult.Value.IpAddress?.ToString() ?? string.Empty,
            deployResult.Value.Ports ?? [],
            deployResult.Value.ContainerName,
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> StopAsync(IDeployableContainer container, CancellationToken cancellationToken)
    {
        var deployService = deployServiceFactory.Create(container);
        if (deployService is null)
            return Error.NotSupported;

        var tags = OperationTags(container, "stop");
        var sw = Stopwatch.StartNew();

        var stopResult = await deployService.StopAsync(container, cancellationToken);
        sw.Stop();

        if (stopResult.IsFailure)
        {
            metrics.ServiceOperations.Add(1, WithResult(tags, "failure"));
            metrics.ServiceOperationDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "failure"));
            return stopResult;
        }

        // Reload guards against a concurrent Docker-event write clobbering the status.
        await unitOfWork.ReloadAsync(container, cancellationToken);

        await MarkStoppedByOperatorAsync(container, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        metrics.ServiceOperations.Add(1, WithResult(tags, "success"));
        metrics.ServiceOperationDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "success"));
        return Result.Success();
    }

    public async Task<Result> StartAsync(IDeployableContainer container, CancellationToken cancellationToken)
    {
        var deployService = deployServiceFactory.Create(container);
        if (deployService is null)
            return Error.NotSupported;

        await MarkTransitioningAsync(container, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tags = OperationTags(container, "start");
        var sw = Stopwatch.StartNew();

        var startResult = await deployService.StartAsync(container, cancellationToken);
        sw.Stop();

        if (startResult.IsFailure)
        {
            await MarkStoppedAfterFailureAsync(container, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            metrics.ServiceOperations.Add(1, WithResult(tags, "failure"));
            metrics.ServiceOperationDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "failure"));
            return startResult.Error;
        }

        if (!await TryMarkDeployedAsync(container, cancellationToken))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            metrics.ServiceOperations.Add(1, WithResult(tags, "failure"));
            metrics.ServiceOperationDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "failure"));
            return Error.Docker.ContainerCrashedAfterStart;
        }

        metrics.ServiceOperations.Add(1, WithResult(tags, "success"));
        metrics.ServiceOperationDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "success"));

        await PersistRegistryUpdateAsync(
            container,
            startResult.Value.IpAddress?.ToString() ?? string.Empty,
            startResult.Value.Ports ?? [],
            startResult.Value.ContainerName,
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RestartAsync(IDeployableContainer container, CancellationToken cancellationToken)
    {
        var deployService = deployServiceFactory.Create(container);
        if (deployService is null)
            return Error.NotSupported;

        await MarkTransitioningAsync(container, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tags = OperationTags(container, "restart");
        var sw = Stopwatch.StartNew();

        var stopResult = await deployService.StopAsync(container, cancellationToken);
        if (stopResult.IsFailure)
        {
            sw.Stop();
            await MarkStoppedAfterFailureAsync(container, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            metrics.ServiceOperations.Add(1, WithResult(tags, "failure"));
            metrics.ServiceOperationDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "failure"));
            return stopResult;
        }

        var startResult = await deployService.StartAsync(container, cancellationToken);
        sw.Stop();

        if (startResult.IsFailure)
        {
            await MarkStoppedAfterFailureAsync(container, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            metrics.ServiceOperations.Add(1, WithResult(tags, "failure"));
            metrics.ServiceOperationDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "failure"));
            return startResult;
        }

        if (!await TryMarkDeployedAsync(container, cancellationToken))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            metrics.ServiceOperations.Add(1, WithResult(tags, "failure"));
            metrics.ServiceOperationDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "failure"));
            return Error.Docker.ContainerCrashedAfterStart;
        }

        metrics.ServiceOperations.Add(1, WithResult(tags, "success"));
        metrics.ServiceOperationDurationSeconds.Record(sw.Elapsed.TotalSeconds, WithResult(tags, "success"));


        await PersistRegistryUpdateAsync(
            container,
            startResult.Value.IpAddress?.ToString() ?? string.Empty,
            startResult.Value.Ports ?? [],
            startResult.Value.ContainerName,
            cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Ensures the container's service-registry entry exists and applies its latest runtime info,
    /// retrying once if a concurrent deploy/start/restart of the same container won the race to
    /// create the entry first (the unique index on service_id/sidecar_id rejects our insert as a
    /// DbUpdateException instead of silently duplicating the row) - in that case we drop our
    /// uncommitted copy and apply the update to the entry that actually landed.
    /// </summary>
    private async Task PersistRegistryUpdateAsync(
        IDeployableContainer container,
        string ipAddress,
        List<Domain.ValueObjects.PortMapping> ports,
        string? containerName,
        CancellationToken cancellationToken)
    {
        var entry = container is Service
            ? await registry.EnsureServiceRegisteredAsync(container.Id, cancellationToken)
            : await registry.EnsureSidecarRegisteredAsync(container.Id, cancellationToken);

        ApplyRunning(container, entry, ipAddress, ports, containerName);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsRegistryUniqueViolation(ex))
        {
            unitOfWork.Detach(entry);

            var winningEntry = container is Service
                ? await registry.EnsureServiceRegisteredAsync(container.Id, cancellationToken)
                : await registry.EnsureSidecarRegisteredAsync(container.Id, cancellationToken);

            ApplyRunning(container, winningEntry, ipAddress, ports, containerName);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsRegistryUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_service_registry_service_id" or "IX_service_registry_sidecar_id"
        };

    /// <summary>
    /// Records the container as deployed unless a reactive Docker event already recorded it as crashed
    /// while this deployment was in flight. Reloading picks up that concurrent write so it isn't clobbered.
    /// </summary>
    private async Task<bool> TryMarkDeployedAsync(IDeployableContainer container, CancellationToken cancellationToken)
    {
        await unitOfWork.ReloadAsync(container, cancellationToken);

        switch (container)
        {
            case Service service:
                var entry = await registry.GetForServiceAsync(service.Id, cancellationToken);
                if (entry is not null)
                {
                    await unitOfWork.ReloadAsync(entry, cancellationToken);
                    if (entry.RuntimeState == ServiceRuntimeState.Crashed)
                        return false;
                }

                // Stamps the deployment time and the operator's intent even if a Docker event already recorded Running.
                service.OnDeploymentSucceeded();
                return true;

            case Sidecar sidecar:
                if (sidecar.Status is ServiceStatus.Stopped)
                    return false;

                sidecar.MarkDeployed();
                sidecar.RecordDeployment();
                return true;

            default:
                return false;
        }
    }

    /// <summary>Marks a Start/Restart in progress so reactive stop events from the old container are ignored.</summary>
    private async Task MarkTransitioningAsync(IDeployableContainer container, CancellationToken ct)
    {
        switch (container)
        {
            case Service service:
                var entry = await registry.GetForServiceAsync(service.Id, ct);
                entry?.ApplyRuntimeState(ServiceRuntimeState.Restarting, DateTime.UtcNow);
                break;
            case Sidecar sidecar:
                sidecar.MarkDeploying();
                break;
        }
    }

    /// <summary>An operator-requested stop: intent changes, and the observed state follows.</summary>
    private async Task MarkStoppedByOperatorAsync(IDeployableContainer container, CancellationToken ct)
    {
        if (container is Service service)
            service.SetDesiredState(DesiredState.Stopped);

        await MarkStoppedAfterFailureAsync(container, ct);
    }

    /// <summary>Records the container as no longer running without touching the operator's intent.</summary>
    private async Task MarkStoppedAfterFailureAsync(IDeployableContainer container, CancellationToken ct)
    {
        switch (container)
        {
            case Service service:
                var serviceEntry = await registry.GetForServiceAsync(service.Id, ct);
                serviceEntry?.ApplyRuntimeState(ServiceRuntimeState.Stopped, DateTime.UtcNow);
                break;
            case Sidecar sidecar:
                sidecar.MarkStopped();
                var sidecarEntry = await registry.GetForSidecarAsync(sidecar.Id, ct);
                sidecarEntry?.ApplyRuntimeState(ServiceRuntimeState.Stopped, DateTime.UtcNow);
                break;
        }
    }

    private static void ApplyRunning(
        IDeployableContainer container,
        ServiceRegistryEntry entry,
        string ipAddress,
        List<Domain.ValueObjects.PortMapping> ports,
        string? containerName)
    {
        entry.UpdateNetwork(ipAddress, ports);
        entry.ContainerName = containerName;
        entry.ApplyRuntimeState(RuntimeStateFor(container), DateTime.UtcNow);
    }

    private static ServiceRuntimeState RuntimeStateFor(IDeployableContainer container) => container switch
    {
        Sidecar sidecar => sidecar.Status switch
        {
            ServiceStatus.Running => ServiceRuntimeState.Running,
            ServiceStatus.Stopped => ServiceRuntimeState.Stopped,
            ServiceStatus.Deploying => ServiceRuntimeState.Restarting,
            ServiceStatus.DeploymentPending => ServiceRuntimeState.NotDeployed,
            _ => ServiceRuntimeState.Unknown
        },
        _ => ServiceRuntimeState.Running
    };

    private static TagList Tags(IDeployableContainer container) => container switch
    {
        Service service => new TagList
        {
            { HavenMetrics.TagService, service.Name },
            { HavenMetrics.TagEnvironment, service.Environment?.Name ?? "unknown" },
            { HavenMetrics.TagProject, service.Environment?.Project?.Name ?? "unknown" },
            { HavenMetrics.TagServiceType, service.Type.ToString() },
        },
        Sidecar sidecar => new TagList
        {
            { HavenMetrics.TagSidecar, sidecar.Name }, { HavenMetrics.TagSidecarKind, sidecar.Kind.ToString() },
        },
        _ => new TagList()
    };

    private static TagList OperationTags(IDeployableContainer container, string operation)
    {
        var tags = Tags(container);
        tags.Add(HavenMetrics.TagOperation, operation);
        return tags;
    }

    private static TagList WithResult(TagList tags, string result)
    {
        tags.Add(HavenMetrics.TagResult, result);
        return tags;
    }
}