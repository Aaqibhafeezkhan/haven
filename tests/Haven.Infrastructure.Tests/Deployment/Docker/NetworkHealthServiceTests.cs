using Docker.DotNet;
using Docker.DotNet.Models;

using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Configuration;
using Haven.Domain.Enums;
using Haven.Infrastructure.Deployment.Docker;
using Haven.Infrastructure.Persistence;
using Haven.Infrastructure.Utils;
using Haven.Testing.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using Network = Haven.Domain.Aggregates.Network;

namespace Haven.Infrastructure.Tests.Deployment.Docker;

[Category("Unit")]
public sealed class NetworkHealthServiceTests
{
    private IDockerClient _client = null!;
    private HavenDbContext _db = null!;
    private INetworkingService _networking = null!;
    private IManifestSyncTrigger _manifestSync = null!;
    private NetworkOptions _options = null!;
    private List<NetworkResponse> _dockerNetworks = null!;
    private NetworkHealthService _sut = null!;

    [SetUp]
    public void Setup()
    {
        _client = Substitute.For<IDockerClient>();
        _db = TestDbContextFactory.CreateUnitDbContext();
        _manifestSync = Substitute.For<IManifestSyncTrigger>();

        _networking = Substitute.For<INetworkingService>();
        _networking.ServiceType.Returns(ServiceType.DockerImage);
        _networking.IsNetworkInUseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _networking.EnsureNetworkExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _networking.RecreateNetworkAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _networking.DeleteNetworkAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        var factory = Substitute.For<INetworkingServiceFactory>();
        factory.Create(ServiceType.DockerImage).Returns(_networking);

        _options = new NetworkOptions();
        var monitor = Substitute.For<IOptionsMonitor<NetworkOptions>>();
        monitor.CurrentValue.Returns(_ => _options);

        _dockerNetworks = [];
        _client.Networks.ListNetworksAsync(Arg.Any<NetworksListParameters>(), Arg.Any<CancellationToken>())
            .Returns(_ => _dockerNetworks.ToList());

        _sut = new NetworkHealthService(_db, _client, factory, monitor, _manifestSync,
            Substitute.For<ILogger<NetworkHealthService>>());
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _client.Dispose();
    }

    private Network SeedSharedWithDockerNetwork(string name, string dockerId, string subnet)
    {
        var network = Network.Create(name, NetworkType.Shared);
        network.SetDockerNetworkId(dockerId);
        network.AssignNetworkInfo(subnet, subnet.Replace(".0/24", ".1"));
        _db.Networks.Add(network);
        _db.SaveChanges();

        _dockerNetworks.Add(new NetworkResponse
        {
            ID = dockerId,
            Name = name,
            IPAM = new IPAM { Config = [new IPAMConfig { Subnet = subnet }] }
        });
        return network;
    }

    [Test]
    public async Task RepairAsync_HealthyNetwork_ChangesNothing()
    {
        SeedSharedWithDockerNetwork("ok", "d1", "10.240.1.0/24");

        var report = await _sut.RepairAsync(dryRun: false, CancellationToken.None);

        report.Entries.ShouldBeEmpty();
        await _networking.DidNotReceive().RecreateNetworkAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _manifestSync.DidNotReceive().RequestSync();
    }

    [Test]
    public async Task RepairAsync_IdleLegacyNetwork_IsRecreatedAndManifestsResynced()
    {
        var network = SeedSharedWithDockerNetwork("legacy", "d1", "172.21.0.0/24");

        var report = await _sut.RepairAsync(dryRun: false, CancellationToken.None);

        report.Entries.Single().Action.ShouldBe(NetworkRepairAction.Repaired);
        await _networking.Received(1).RecreateNetworkAsync(network.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
        _manifestSync.Received(1).RequestSync();
    }

    [Test]
    public async Task RepairAsync_LegacyNetworkInUse_IsSkippedAndNeverRecreated()
    {
        var network = SeedSharedWithDockerNetwork("busy", "d1", "172.21.0.0/16");
        _networking.IsNetworkInUseAsync(network.Id, Arg.Any<CancellationToken>()).Returns(true);

        var report = await _sut.RepairAsync(dryRun: false, CancellationToken.None);

        report.Entries.Single().Action.ShouldBe(NetworkRepairAction.SkippedInUse);
        await _networking.DidNotReceive().RecreateNetworkAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RepairAsync_WhenRaceAttachesContainer_ReportsSkippedInsteadOfFailed()
    {
        SeedSharedWithDockerNetwork("race", "d1", "172.21.0.0/16");
        _networking.RecreateNetworkAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Error.Docker.NetworkInUse);

        var report = await _sut.RepairAsync(dryRun: false, CancellationToken.None);

        report.Entries.Single().Action.ShouldBe(NetworkRepairAction.SkippedInUse);
    }

    [Test]
    public async Task RepairAsync_AutoRepairDisabled_ReportsButDoesNotTouch()
    {
        _options.AutoRepairIdleNetworks = false;
        SeedSharedWithDockerNetwork("legacy", "d1", "172.21.0.0/16");

        var report = await _sut.RepairAsync(dryRun: false, CancellationToken.None);

        report.Entries.Single().Action.ShouldBe(NetworkRepairAction.SkippedDisabled);
        await _networking.DidNotReceive().RecreateNetworkAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RepairAsync_DryRun_ReportsWouldRepairWithoutChanges()
    {
        SeedSharedWithDockerNetwork("legacy", "d1", "172.21.0.0/16");

        var report = await _sut.RepairAsync(dryRun: true, CancellationToken.None);

        report.Entries.Single().Action.ShouldBe(NetworkRepairAction.WouldRepair);
        await _networking.DidNotReceive().RecreateNetworkAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _manifestSync.DidNotReceive().RequestSync();
    }

    [Test]
    public async Task RepairAsync_StaleDockerId_IsReprovisionedThroughEnsure()
    {
        var network = Network.Create("stale", NetworkType.Shared);
        network.SetDockerNetworkId("vanished");
        _db.Networks.Add(network);
        await _db.SaveChangesAsync();

        var report = await _sut.RepairAsync(dryRun: false, CancellationToken.None);

        report.Entries.Single().Action.ShouldBe(NetworkRepairAction.Repaired);
        await _networking.Received(1).EnsureNetworkExistsAsync(network.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RepairAsync_OneFailureDoesNotStopTheRest()
    {
        var first = SeedSharedWithDockerNetwork("a", "d1", "172.21.0.0/16");
        var second = SeedSharedWithDockerNetwork("b", "d2", "172.22.0.0/16");
        _networking.RecreateNetworkAsync(first.Id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Error.Docker.FailedToCreateNetwork);

        var report = await _sut.RepairAsync(dryRun: false, CancellationToken.None);

        report.Entries.Count.ShouldBe(2);
        report.Entries.Single(e => e.Network.NetworkId == first.Id).Action.ShouldBe(NetworkRepairAction.Failed);
        report.Entries.Single(e => e.Network.NetworkId == second.Id).Action.ShouldBe(NetworkRepairAction.Repaired);
    }

    [Test]
    public async Task RepairAsync_OrphanedDockerNetwork_IsOnlyRemovedWhenEnabledAndPastGracePeriod()
    {
        var labels = new Dictionary<string, string>
        {
            { DockerUtils.HavenManagedLabel.Key, DockerUtils.HavenManagedLabel.Value },
            { "haven.created-at", DateTime.UtcNow.AddDays(-3).ToString("O") }
        };
        _dockerNetworks.Add(new NetworkResponse { ID = "orphan", Name = "haven-gone-prod", Labels = labels });
        _client.Networks.InspectNetworkAsync("orphan", Arg.Any<CancellationToken>())
            .Returns(new NetworkResponse { ID = "orphan", Containers = new Dictionary<string, EndpointResource>() });

        var disabled = await _sut.RepairAsync(dryRun: false, CancellationToken.None);
        disabled.Entries.Single().Action.ShouldBe(NetworkRepairAction.SkippedDisabled);
        await _client.Networks.DidNotReceive().DeleteNetworkAsync("orphan", Arg.Any<CancellationToken>());

        _options.RemoveOrphanedDockerNetworks = true;
        var enabled = await _sut.RepairAsync(dryRun: false, CancellationToken.None);
        enabled.Entries.Single().Action.ShouldBe(NetworkRepairAction.Repaired);
        await _client.Networks.Received(1).DeleteNetworkAsync("orphan", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RepairAsync_RecentOrphanedDockerNetwork_IsLeftAloneDuringGracePeriod()
    {
        _options.RemoveOrphanedDockerNetworks = true;
        var labels = new Dictionary<string, string>
        {
            { DockerUtils.HavenManagedLabel.Key, DockerUtils.HavenManagedLabel.Value },
            { "haven.created-at", DateTime.UtcNow.AddMinutes(-5).ToString("O") }
        };
        _dockerNetworks.Add(new NetworkResponse { ID = "fresh", Name = "haven-new", Labels = labels });

        var report = await _sut.RepairAsync(dryRun: false, CancellationToken.None);

        report.Entries.Single().Action.ShouldBe(NetworkRepairAction.SkippedDisabled);
        await _client.Networks.DidNotReceive().DeleteNetworkAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}