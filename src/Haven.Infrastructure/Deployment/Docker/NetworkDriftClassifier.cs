using Docker.DotNet.Models;

using Haven.Application.Common.Interfaces.Deployment;
using Haven.Domain.Enums;
using Haven.Infrastructure.Utils;

using Network = Haven.Domain.Aggregates.Network;

namespace Haven.Infrastructure.Deployment.Docker;

/// <summary>
/// Pure comparison of Haven's network records against what Docker actually has. No I/O, so every
/// drift case is unit-testable and the health report, the repair pass and the deploy path can't
/// disagree about what "drifted" means.
/// </summary>
public static class NetworkDriftClassifier
{
    public static (NetworkDriftState State, string Reason) Classify(
        Network network, IReadOnlyCollection<NetworkResponse> dockerNetworks, string subnetPool)
    {
        if (network.Type == NetworkType.ProjectEnvironment && (network.ProjectId is null || network.EnvironmentId is null))
            return (NetworkDriftState.OrphanedRecord, "The project or environment this network belonged to no longer exists.");

        var expectedName = DockerUtils.SanitizeForDocker(network.Name);
        var hasId = !string.IsNullOrEmpty(network.DockerNetworkId);

        var byId = hasId ? dockerNetworks.FirstOrDefault(n => n.ID == network.DockerNetworkId) : null;
        if (byId is null)
        {
            var byName = dockerNetworks.FirstOrDefault(n => n.Name == expectedName);
            if (byName is not null)
                return (NetworkDriftState.Adoptable,
                    $"Docker already has a network named '{expectedName}' but Haven records " +
                    (hasId ? "a different id for it." : "no id for it."));

            return hasId
                ? (NetworkDriftState.StaleDockerId, $"The recorded Docker network {network.DockerNetworkId![..Math.Min(12, network.DockerNetworkId.Length)]} no longer exists.")
                : (NetworkDriftState.NotProvisioned, "No Docker network has been created yet.");
        }

        var outside = SubnetsOf(byId).FirstOrDefault(s => !SubnetAllocator.IsWithin(s, subnetPool));
        return outside is null
            ? (NetworkDriftState.Healthy, "OK")
            : (NetworkDriftState.LegacySubnet, $"Subnet {outside} is outside the configured pool {subnetPool}.");
    }

    /// <summary>
    /// Docker networks carrying Haven's managed label that no Haven network record refers to, by id or name.
    /// </summary>
    public static IReadOnlyList<NetworkResponse> FindOrphanedDockerNetworks(
        IReadOnlyCollection<Network> networks, IReadOnlyCollection<NetworkResponse> dockerNetworks)
    {
        var managed = DockerUtils.HavenManagedLabel;
        var knownIds = networks.Where(n => !string.IsNullOrEmpty(n.DockerNetworkId)).Select(n => n.DockerNetworkId!).ToHashSet();
        var knownNames = networks.Select(n => DockerUtils.SanitizeForDocker(n.Name)).ToHashSet();

        return dockerNetworks
            .Where(d => d.Labels is not null
                        && d.Labels.TryGetValue(managed.Key, out var value) && value == managed.Value
                        && !knownIds.Contains(d.ID) && !knownNames.Contains(d.Name))
            .ToList();
    }

    public static IEnumerable<string> SubnetsOf(NetworkResponse network) =>
        (network.IPAM?.Config ?? [])
        .Select(c => c.Subnet)
        .Where(s => !string.IsNullOrWhiteSpace(s));
}