using Docker.DotNet;
using Docker.DotNet.Models;

using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Configuration;
using Haven.Domain;
using Haven.Domain.Enums;
using Haven.Infrastructure.Persistence;
using Haven.Infrastructure.Utils;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Haven.Infrastructure.Deployment.Docker;

/// <inheritdoc cref="INetworkHealthService" />
public sealed class NetworkHealthService(
    HavenDbContext dbContext,
    IDockerClient dockerClient,
    INetworkingServiceFactory networkingServiceFactory,
    IOptionsMonitor<NetworkOptions> networkOptions,
    IManifestSyncTrigger manifestSyncTrigger,
    ILogger<NetworkHealthService> logger) : INetworkHealthService
{
    // A repair pass already recreates networks one at a time; never let two passes (hourly job + an
    // admin-triggered run) overlap.
    private static readonly SemaphoreSlim RepairGate = new(1, 1);

    public async Task<NetworkHealthReport> AssessAsync(CancellationToken cancellationToken)
    {
        var (pool, entries, _) = await BuildEntriesAsync(cancellationToken);
        return new NetworkHealthReport(pool, entries.Select(e => e.Entry).ToList());
    }

    public async Task<NetworkRepairReport> RepairAsync(bool dryRun, CancellationToken cancellationToken)
    {
        await RepairGate.WaitAsync(cancellationToken);
        try
        {
            var (_, entries, _) = await BuildEntriesAsync(cancellationToken);
            var networkingService = networkingServiceFactory.Create(ServiceType.DockerImage);
            var results = new List<NetworkRepairEntry>();

            foreach (var (entry, dockerNetwork) in entries.Where(e => IsDrifted(e.Entry.State)))
            {
                if (!entry.AutoRepairable)
                {
                    results.Add(new NetworkRepairEntry(entry,
                        entry.InUse ? NetworkRepairAction.SkippedInUse : NetworkRepairAction.SkippedDisabled,
                        entry.InUse
                            ? "Containers are still attached; it will be repaired once the network is idle."
                            : "Automatic repair is disabled, or the network is still within its grace period."));
                    continue;
                }

                if (dryRun)
                {
                    results.Add(new NetworkRepairEntry(entry, NetworkRepairAction.WouldRepair, entry.Reason));
                    continue;
                }

                results.Add(await RepairEntryAsync(entry, dockerNetwork, networkingService, cancellationToken));
            }

            if (!dryRun && results.Any(r => r.Action == NetworkRepairAction.Repaired))
                manifestSyncTrigger.RequestSync();

            return new NetworkRepairReport(dryRun, results);
        }
        finally
        {
            RepairGate.Release();
        }
    }

    private async Task<NetworkRepairEntry> RepairEntryAsync(NetworkHealthEntry entry, NetworkResponse? dockerNetwork,
        INetworkingService? networkingService, CancellationToken cancellationToken)
    {
        try
        {
            if (entry.State == NetworkDriftState.OrphanedDockerNetwork)
            {
                await dockerClient.Networks.DeleteNetworkAsync(dockerNetwork!.ID, cancellationToken);
                logger.LogInformation("Network health: removed orphaned Docker network {Name} ({Id})",
                    entry.Name, dockerNetwork.ID);
                return new NetworkRepairEntry(entry, NetworkRepairAction.Repaired, "Removed the orphaned Docker network.");
            }

            if (networkingService is null || entry.NetworkId is not { } networkId)
                return new NetworkRepairEntry(entry, NetworkRepairAction.Failed, "No Docker networking service is available.");

            Result result;
            switch (entry.State)
            {
                case NetworkDriftState.StaleDockerId or NetworkDriftState.Adoptable:
                    result = await networkingService.EnsureNetworkExistsAsync(networkId, cancellationToken);
                    break;

                case NetworkDriftState.LegacySubnet:
                    result = await networkingService.RecreateNetworkAsync(networkId, entry.Reason, cancellationToken);
                    if (result.IsFailure && result.Error == Error.Docker.NetworkInUse)
                        return new NetworkRepairEntry(entry, NetworkRepairAction.SkippedInUse,
                            "A container attached while the repair was starting; it will be retried on the next pass.");
                    break;

                case NetworkDriftState.OrphanedRecord:
                    result = await RemoveOrphanedRecordAsync(networkId, networkingService, cancellationToken);
                    break;

                default:
                    return new NetworkRepairEntry(entry, NetworkRepairAction.None, null);
            }

            if (result.IsFailure)
            {
                logger.LogWarning("Network health: repair of {Name} ({State}) failed: {Error}",
                    entry.Name, entry.State, result.Error.Message);
                return new NetworkRepairEntry(entry, NetworkRepairAction.Failed, result.Error.Message);
            }

            logger.LogInformation("Network health: repaired {Name} ({State}): {Reason}", entry.Name, entry.State, entry.Reason);
            return new NetworkRepairEntry(entry, NetworkRepairAction.Repaired, entry.Reason);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Network health: repair of {Name} ({State}) threw", entry.Name, entry.State);
            return new NetworkRepairEntry(entry, NetworkRepairAction.Failed, ex.Message);
        }
    }

    private async Task<Result> RemoveOrphanedRecordAsync(Guid networkId, INetworkingService networkingService,
        CancellationToken cancellationToken)
    {
        // Re-checked right before removal: a container may have attached since the assessment.
        if (await networkingService.IsNetworkInUseAsync(networkId, cancellationToken))
            return Error.Docker.NetworkInUse;

        var deleteResult = await networkingService.DeleteNetworkAsync(networkId, cancellationToken);
        if (deleteResult.IsFailure)
            return deleteResult;

        var network = await dbContext.Networks.FirstOrDefaultAsync(n => n.Id == networkId, cancellationToken);
        if (network is null)
            return Result.Success();

        dbContext.ServiceNetworks.RemoveRange(dbContext.ServiceNetworks.Where(sn => sn.NetworkId == networkId));
        dbContext.SidecarNetworks.RemoveRange(dbContext.SidecarNetworks.Where(sn => sn.NetworkId == networkId));
        network.Delete();
        dbContext.Networks.Remove(network);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<(string Pool, List<(NetworkHealthEntry Entry, NetworkResponse? Docker)> Entries, bool Unused)>
        BuildEntriesAsync(CancellationToken cancellationToken)
    {
        var options = networkOptions.CurrentValue;
        var pool = SubnetAllocator.TryParseCidr(options.SubnetPool, out _, out var prefix) && prefix <= SubnetAllocator.SubnetPrefixLength
            ? options.SubnetPool
            : SubnetAllocator.DefaultPool;

        var networks = await dbContext.Networks
            .Where(n => n.Type != NetworkType.External)
            .ToListAsync(cancellationToken);
        var dockerNetworks = await dockerClient.Networks.ListNetworksAsync(new NetworksListParameters(), cancellationToken);
        var networkingService = networkingServiceFactory.Create(ServiceType.DockerImage);

        var entries = new List<(NetworkHealthEntry, NetworkResponse?)>();

        foreach (var network in networks)
        {
            var (state, reason) = NetworkDriftClassifier.Classify(network, dockerNetworks.ToList(), pool);

            var inUse = false;
            if (state is NetworkDriftState.LegacySubnet or NetworkDriftState.OrphanedRecord && networkingService is not null)
                inUse = await networkingService.IsNetworkInUseAsync(network.Id, cancellationToken);

            var autoRepairable = state switch
            {
                NetworkDriftState.StaleDockerId or NetworkDriftState.Adoptable => true,
                NetworkDriftState.LegacySubnet => options.AutoRepairIdleNetworks && !inUse,
                NetworkDriftState.OrphanedRecord => !inUse,
                _ => false
            };

            entries.Add((new NetworkHealthEntry(network.Id, network.Name, network.Type.ToString(), network.DockerNetworkId,
                network.Subnet, state, reason, inUse, autoRepairable), null));
        }

        foreach (var orphan in NetworkDriftClassifier.FindOrphanedDockerNetworks(networks, dockerNetworks.ToList()))
        {
            var inUse = await IsDockerNetworkInUseAsync(orphan.ID, cancellationToken);
            var pastGrace = IsPastGracePeriod(orphan, options.OrphanNetworkGracePeriodHours);

            entries.Add((new NetworkHealthEntry(null, orphan.Name, "Docker", orphan.ID,
                    NetworkDriftClassifier.SubnetsOf(orphan).FirstOrDefault(), NetworkDriftState.OrphanedDockerNetwork,
                    "A Haven-managed Docker network that no Haven network record refers to.", inUse,
                    options.RemoveOrphanedDockerNetworks && pastGrace && !inUse), orphan));
        }

        return (pool, entries, false);
    }

    private async Task<bool> IsDockerNetworkInUseAsync(string dockerNetworkId, CancellationToken cancellationToken)
    {
        try
        {
            var inspect = await dockerClient.Networks.InspectNetworkAsync(dockerNetworkId, cancellationToken);
            return inspect?.Containers is { Count: > 0 };
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static bool IsPastGracePeriod(NetworkResponse network, int graceHours)
    {
        // Without a readable creation time we can't prove the network is old enough; stay conservative.
        if (network.Labels is null
            || !network.Labels.TryGetValue("haven.created-at", out var raw)
            || !DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var createdAt))
            return false;

        return DateTime.UtcNow - createdAt.ToUniversalTime() >= TimeSpan.FromHours(Math.Max(0, graceHours));
    }

    private static bool IsDrifted(NetworkDriftState state) =>
        state is not (NetworkDriftState.Healthy or NetworkDriftState.NotProvisioned);
}