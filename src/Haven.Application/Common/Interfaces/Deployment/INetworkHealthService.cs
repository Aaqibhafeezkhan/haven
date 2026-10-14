namespace Haven.Application.Common.Interfaces.Deployment;

public enum NetworkDriftState
{
    /// <summary>The Docker network exists and its subnet is inside the configured pool.</summary>
    Healthy,

    /// <summary>No Docker network exists yet (not deployed to). Provisioned lazily; nothing to repair.</summary>
    NotProvisioned,

    /// <summary>The stored Docker network id no longer exists in Docker.</summary>
    StaleDockerId,

    /// <summary>Docker has a network with this name but Haven holds no (or a different) id for it.</summary>
    Adoptable,

    /// <summary>The Docker network's subnet is outside the configured pool.</summary>
    LegacySubnet,

    /// <summary>A project/environment network whose project or environment no longer exists.</summary>
    OrphanedRecord,

    /// <summary>A Haven-managed Docker network that no Haven network record refers to.</summary>
    OrphanedDockerNetwork
}

public enum NetworkRepairAction
{
    None,
    Repaired,
    WouldRepair,
    SkippedInUse,
    SkippedDisabled,
    Failed
}

public sealed record NetworkHealthEntry(
    Guid? NetworkId,
    string Name,
    string Type,
    string? DockerNetworkId,
    string? Subnet,
    NetworkDriftState State,
    string Reason,
    bool InUse,
    bool AutoRepairable);

public sealed record NetworkHealthReport(string SubnetPool, IReadOnlyList<NetworkHealthEntry> Networks)
{
    public int DriftedCount => Networks.Count(n => n.State is not (NetworkDriftState.Healthy or NetworkDriftState.NotProvisioned));
}

public sealed record NetworkRepairEntry(
    NetworkHealthEntry Network,
    NetworkRepairAction Action,
    string? Message);

public sealed record NetworkRepairReport(bool DryRun, IReadOnlyList<NetworkRepairEntry> Entries)
{
    public int RepairedCount => Entries.Count(e => e.Action == NetworkRepairAction.Repaired);
}

/// <summary>
/// Compares Haven's network records with what Docker actually has and repairs drift: stale or
/// missing Docker ids, networks on subnets outside the configured pool, and orphans. Safe to run
/// repeatedly; every repair is idempotent and never touches a network that has containers attached.
/// </summary>
public interface INetworkHealthService
{
    Task<NetworkHealthReport> AssessAsync(CancellationToken cancellationToken);
    Task<NetworkRepairReport> RepairAsync(bool dryRun, CancellationToken cancellationToken);
}