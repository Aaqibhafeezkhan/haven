using Docker.DotNet.Models;

using Haven.Application.Common.Interfaces.Deployment;
using Haven.Domain.Enums;
using Haven.Infrastructure.Deployment.Docker;
using Haven.Infrastructure.Utils;

using Shouldly;

using Network = Haven.Domain.Aggregates.Network;

namespace Haven.Infrastructure.Tests.Deployment.Docker;

[Category("Unit")]
public sealed class NetworkDriftClassifierTests
{
    private const string Pool = "10.240.0.0/12";

    private static Network SharedNetwork(string name = "shared-net", string? dockerId = null)
    {
        var network = Network.Create(name, NetworkType.Shared);
        if (dockerId is not null) network.SetDockerNetworkId(dockerId);
        return network;
    }

    private static NetworkResponse DockerNet(string id, string name, string? subnet = null,
        Dictionary<string, string>? labels = null) => new()
    {
        ID = id,
        Name = name,
        Labels = labels,
        IPAM = subnet is null ? null : new IPAM { Config = [new IPAMConfig { Subnet = subnet }] }
    };

    [Test]
    public void Classify_NoIdAndNoDockerNetwork_IsNotProvisioned() =>
        NetworkDriftClassifier.Classify(SharedNetwork(), [], Pool).State.ShouldBe(NetworkDriftState.NotProvisioned);

    [Test]
    public void Classify_IdMissingFromDocker_IsStale() =>
        NetworkDriftClassifier.Classify(SharedNetwork(dockerId: "gone"), [DockerNet("other", "other")], Pool)
            .State.ShouldBe(NetworkDriftState.StaleDockerId);

    [Test]
    public void Classify_NoIdButExactNameInDocker_IsAdoptable() =>
        NetworkDriftClassifier.Classify(SharedNetwork(), [DockerNet("d1", "shared-net", "10.240.1.0/24")], Pool)
            .State.ShouldBe(NetworkDriftState.Adoptable);

    [Test]
    public void Classify_NameThatOnlyContainsOursIsNotAdoptable() =>
        NetworkDriftClassifier.Classify(SharedNetwork(), [DockerNet("d1", "my-shared-net-2")], Pool)
            .State.ShouldBe(NetworkDriftState.NotProvisioned);

    [Test]
    public void Classify_StaleIdButNameStillPresent_IsAdoptable() =>
        NetworkDriftClassifier.Classify(SharedNetwork(dockerId: "old"), [DockerNet("new", "shared-net")], Pool)
            .State.ShouldBe(NetworkDriftState.Adoptable);

    [TestCase("10.240.5.0/24", NetworkDriftState.Healthy)]
    [TestCase("10.255.255.0/24", NetworkDriftState.Healthy)]
    [TestCase("172.21.0.0/16", NetworkDriftState.LegacySubnet)]
    [TestCase("172.29.33.0/24", NetworkDriftState.LegacySubnet)]
    [TestCase("10.0.0.0/8", NetworkDriftState.LegacySubnet)]
    public void Classify_ExistingNetwork_JudgedBySubnetAgainstPool(string subnet, NetworkDriftState expected) =>
        NetworkDriftClassifier.Classify(SharedNetwork(dockerId: "d1"), [DockerNet("d1", "shared-net", subnet)], Pool)
            .State.ShouldBe(expected);

    [Test]
    public void Classify_ExistingNetworkWithoutIpam_IsHealthy() =>
        NetworkDriftClassifier.Classify(SharedNetwork(dockerId: "d1"), [DockerNet("d1", "shared-net")], Pool)
            .State.ShouldBe(NetworkDriftState.Healthy);

    [Test]
    public void Classify_ProjectEnvironmentNetworkWithoutOwner_IsOrphanedRecord()
    {
        var network = Network.CreateProjectEnvironmentNetwork(Guid.NewGuid(), "p", Guid.NewGuid(), "e");
        typeof(Network).GetProperty(nameof(Network.ProjectId))!.SetValue(network, null);

        NetworkDriftClassifier.Classify(network, [], Pool).State.ShouldBe(NetworkDriftState.OrphanedRecord);
    }

    [Test]
    public void FindOrphanedDockerNetworks_OnlyReturnsManagedNetworksNoRecordRefersTo()
    {
        var managed = new Dictionary<string, string> { { DockerUtils.HavenManagedLabel.Key, DockerUtils.HavenManagedLabel.Value } };
        var known = SharedNetwork("known", "id-known");
        var byName = SharedNetwork("by-name");

        var orphans = NetworkDriftClassifier.FindOrphanedDockerNetworks(
            [known, byName],
            [
                DockerNet("id-known", "known", labels: managed),
                DockerNet("x1", "by-name", labels: managed),
                DockerNet("x2", "orphan", labels: managed),
                DockerNet("x3", "unmanaged-third-party"),
                DockerNet("x4", "bridge", labels: new Dictionary<string, string>())
            ]);

        orphans.Select(o => o.ID).ShouldBe(["x2"]);
    }
}
