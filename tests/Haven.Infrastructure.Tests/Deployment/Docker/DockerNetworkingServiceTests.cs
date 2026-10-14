using Docker.DotNet;
using Docker.DotNet.Models;

using Haven.Application.Common;
using Haven.Application.Configuration;
using Haven.Domain;
using Haven.Domain.Aggregates;
using Haven.Domain.Entities;
using Haven.Domain.Enums;
using Haven.Infrastructure.Deployment.Docker;
using Haven.Infrastructure.Persistence;
using Haven.Testing.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using Network = Haven.Domain.Aggregates.Network;

namespace Haven.Infrastructure.Tests.Deployment.Docker;

[Category("Unit")]
public sealed class DockerNetworkingServiceTests
{
    private IDockerClient _client = null!;
    private HavenDbContext _db = null!;
    private ILogger<DockerNetworkingService> _logger = null!;
    private IOptionsMonitor<NetworkOptions> _networkOptions = null!;
    private DockerNetworkingService _sut = null!;

    [SetUp]
    public void Setup()
    {
        _client = Substitute.For<IDockerClient>();
        _db = TestDbContextFactory.CreateUnitDbContext();
        _logger = Substitute.For<ILogger<DockerNetworkingService>>();
        _networkOptions = Substitute.For<IOptionsMonitor<NetworkOptions>>();
        _networkOptions.CurrentValue.Returns(new NetworkOptions());
        _sut = new DockerNetworkingService(_db, _logger, _client, _networkOptions, new NetworkLockProvider());

        // No container exists for the service in any of these tests.
        _client.Containers.ListContainersAsync(Arg.Any<ContainersListParameters>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _client.Dispose();
    }

    private (Project Project, Service Service) SeedServiceWithoutContainer()
    {
        var project = Project.Create("acme", alias: "acme");
        var environment = project.AddEnvironment("prod", alias: "prod");
        var service = project.AddService(environment.Id, "api", ServiceType.DockerImage, ExposureMode.None, alias: "api");
        _db.Projects.Add(project);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return (project, service);
    }

    [Test]
    public async Task ConnectServiceToNetworksAsync_WhenNoContainerExists_StillPersistsMembership()
    {
        var (_, service) = SeedServiceWithoutContainer();
        var network = Network.Create("shared-net", NetworkType.Shared);
        network.SetDockerNetworkId("docker-net-1");
        _db.Networks.Add(network);
        await _db.SaveChangesAsync();

        var result = await _sut.ConnectServiceToNetworksAsync(service.Id, [network.Id], CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var serviceNetwork = await _db.ServiceNetworks.FindAsync(service.Id, network.Id);
        serviceNetwork.ShouldNotBeNull();

        await _client.Networks.DidNotReceive().ConnectNetworkAsync(
            Arg.Any<string>(), Arg.Any<NetworkConnectParameters>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DisconnectServiceFromNetworksAsync_WhenNoContainerExists_StillRemovesMembership()
    {
        var (_, service) = SeedServiceWithoutContainer();
        var network = Network.Create("shared-net", NetworkType.Shared);
        network.SetDockerNetworkId("docker-net-1");
        _db.Networks.Add(network);
        _db.ServiceNetworks.Add(ServiceNetwork.Create(service.Id, network.Id));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await _sut.DisconnectServiceFromNetworksAsync(service.Id, [network.Id], CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var serviceNetwork = await _db.ServiceNetworks.FindAsync(service.Id, network.Id);
        serviceNetwork.ShouldBeNull();
    }

    [Test]
    public async Task ConnectServiceToNetworksAsync_WhenContainerExists_PerformsLiveConnect()
    {
        var (_, service) = SeedServiceWithoutContainer();
        var network = Network.Create("shared-net", NetworkType.Shared);
        network.SetDockerNetworkId("docker-net-1");
        _db.Networks.Add(network);
        await _db.SaveChangesAsync();

        _client.Containers.ListContainersAsync(Arg.Any<ContainersListParameters>(), Arg.Any<CancellationToken>())
            .Returns([new ContainerListResponse { ID = "container-1" }]);
        _client.Networks.InspectNetworkAsync("docker-net-1", Arg.Any<CancellationToken>())
            .Returns(new NetworkResponse());

        var result = await _sut.ConnectServiceToNetworksAsync(service.Id, [network.Id], CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _client.Networks.Received(1).ConnectNetworkAsync(
            "docker-net-1",
            Arg.Is<NetworkConnectParameters>(p => p.Container == "container-1"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DisconnectServiceFromAllNetworksAsync_PreservesSharedAndExternalMembership_OnlyRemovesProjectEnvironment()
    {
        var (project, service) = SeedServiceWithoutContainer();
        var environment = project.Environments.Single();

        var envNetwork = Network.CreateProjectEnvironmentNetwork(project.Id, "acme", environment.Id, "prod");
        envNetwork.SetDockerNetworkId("docker-net-env");
        var sharedNetwork = Network.Create("shared-net", NetworkType.Shared);
        sharedNetwork.SetDockerNetworkId("docker-net-shared");
        var externalNetwork = Network.Create("external-net", NetworkType.External);
        externalNetwork.SetDockerNetworkId("docker-net-external");

        _db.Networks.AddRange(envNetwork, sharedNetwork, externalNetwork);
        _db.ServiceNetworks.AddRange(
            ServiceNetwork.Create(service.Id, envNetwork.Id),
            ServiceNetwork.Create(service.Id, sharedNetwork.Id),
            ServiceNetwork.Create(service.Id, externalNetwork.Id));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Container is running, so the live Docker disconnect path also runs - harmless/best-effort.
        _client.Containers.ListContainersAsync(Arg.Any<ContainersListParameters>(), Arg.Any<CancellationToken>())
            .Returns([new ContainerListResponse { ID = "container-1" }]);

        var result = await _sut.DisconnectServiceFromAllNetworksAsync(service.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var remaining = await _db.ServiceNetworks
            .Where(sn => sn.ServiceId == service.Id)
            .Select(sn => sn.NetworkId)
            .ToListAsync();

        remaining.ShouldNotContain(envNetwork.Id);
        remaining.ShouldContain(sharedNetwork.Id);
        remaining.ShouldContain(externalNetwork.Id);
    }

    [Test]
    public async Task CreateProjectEnvironmentNetworkAsync_AvoidsSubnetsInsideExistingWiderNetworks()
    {
        var (project, _) = SeedServiceWithoutContainer();
        var environmentId = (await _db.Environments.FirstAsync()).Id;

        _client.Networks.ListNetworksAsync(Arg.Any<NetworksListParameters>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<NetworksListParameters>().Filters is null
                ? [new NetworkResponse { Name = "other", IPAM = new IPAM { Config = [new IPAMConfig { Subnet = "10.240.0.0/16" }] } }]
                : []);
        NetworksCreateParameters? sent = null;
        _client.Networks.CreateNetworkAsync(Arg.Do<NetworksCreateParameters>(p => sent = p), Arg.Any<CancellationToken>())
            .Returns(new NetworksCreateResponse { ID = "docker-1" });

        var result = await _sut.CreateProjectEnvironmentNetworkAsync(project.Id, environmentId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        sent.ShouldNotBeNull();
        sent.IPAM.Config.Single().Subnet.ShouldBe("10.241.0.0/24");
        sent.IPAM.Config.Single().Gateway.ShouldBe("10.241.0.1");
        var saved = await _db.Networks.SingleAsync();
        saved.Subnet.ShouldBe("10.241.0.0/24");
        saved.DockerNetworkId.ShouldBe("docker-1");
    }

    [Test]
    public async Task CreateProjectEnvironmentNetworkAsync_WhenDockerReportsOverlap_RetriesWithNextSubnet()
    {
        var (project, _) = SeedServiceWithoutContainer();
        var environmentId = (await _db.Environments.FirstAsync()).Id;

        _client.Networks.ListNetworksAsync(Arg.Any<NetworksListParameters>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var subnets = new List<string>();
        _client.Networks.CreateNetworkAsync(Arg.Any<NetworksCreateParameters>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                subnets.Add(call.Arg<NetworksCreateParameters>().IPAM.Config.Single().Subnet);
                return subnets.Count == 1
                    ? throw new DockerApiException(System.Net.HttpStatusCode.Forbidden,
                        "Pool overlaps with other one on this address space")
                    : Task.FromResult(new NetworksCreateResponse { ID = "docker-2" });
            });

        var result = await _sut.CreateProjectEnvironmentNetworkAsync(project.Id, environmentId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        subnets.ShouldBe(["10.240.0.0/24", "10.240.1.0/24"]);
    }

    [Test]
    public async Task CreateProjectEnvironmentNetworkAsync_WhenPoolExhausted_ReturnsNoFreeSubnet()
    {
        var (project, _) = SeedServiceWithoutContainer();
        var environmentId = (await _db.Environments.FirstAsync()).Id;
        _networkOptions.CurrentValue.Returns(new NetworkOptions { SubnetPool = "10.250.0.0/24" });

        _client.Networks.ListNetworksAsync(Arg.Any<NetworksListParameters>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<NetworksListParameters>().Filters is null
                ? [new NetworkResponse { Name = "other", IPAM = new IPAM { Config = [new IPAMConfig { Subnet = "10.250.0.0/24" }] } }]
                : []);

        var result = await _sut.CreateProjectEnvironmentNetworkAsync(project.Id, environmentId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Error.Docker.NoFreeSubnet);
    }

    private Network SeedSharedNetwork(string name, string? dockerId, string? subnet = null, NetworkType type = NetworkType.Shared)
    {
        var network = Network.Create(name, type);
        if (dockerId is not null) network.SetDockerNetworkId(dockerId);
        if (subnet is not null) network.AssignNetworkInfo(subnet, "172.21.0.1");
        _db.Networks.Add(network);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return network;
    }

    private static DockerApiException NotFound() => new(System.Net.HttpStatusCode.NotFound, "no such network");

    private void NoDockerNetworksExist() =>
        _client.Networks.ListNetworksAsync(Arg.Any<NetworksListParameters>(), Arg.Any<CancellationToken>()).Returns([]);

    [Test]
    public async Task EnsureNetworkExistsAsync_WhenRecordedDockerNetworkIsGone_ProvisionsANewOneInThePool()
    {
        var network = SeedSharedNetwork("shared-net", "vanished", "172.21.0.0/16");
        _client.Networks.InspectNetworkAsync("vanished", Arg.Any<CancellationToken>()).Returns(Task.FromException<NetworkResponse>(NotFound()));
        NoDockerNetworksExist();
        _client.Networks.CreateNetworkAsync(Arg.Any<NetworksCreateParameters>(), Arg.Any<CancellationToken>())
            .Returns(new NetworksCreateResponse { ID = "fresh" });

        var result = await _sut.EnsureNetworkExistsAsync(network.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var saved = await _db.Networks.SingleAsync();
        saved.DockerNetworkId.ShouldBe("fresh");
        saved.Subnet.ShouldBe("10.240.0.0/24");
    }

    [Test]
    public async Task EnsureNetworkExistsAsync_WhenRecordedDockerNetworkStillExists_DoesNotCreateAnything()
    {
        var network = SeedSharedNetwork("shared-net", "alive", "10.240.3.0/24");
        _client.Networks.InspectNetworkAsync("alive", Arg.Any<CancellationToken>()).Returns(new NetworkResponse { ID = "alive" });

        var result = await _sut.EnsureNetworkExistsAsync(network.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _client.Networks.DidNotReceive().CreateNetworkAsync(Arg.Any<NetworksCreateParameters>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnsureNetworkExistsAsync_AdoptingByName_RecordsTheExistingSubnet()
    {
        var network = SeedSharedNetwork("shared-net", dockerId: null);
        _client.Networks.ListNetworksAsync(Arg.Any<NetworksListParameters>(), Arg.Any<CancellationToken>())
            .Returns([new NetworkResponse
            {
                ID = "existing", Name = "shared-net",
                IPAM = new IPAM { Config = [new IPAMConfig { Subnet = "10.240.9.0/24", Gateway = "10.240.9.1" }] }
            }]);

        var result = await _sut.EnsureNetworkExistsAsync(network.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var saved = await _db.Networks.SingleAsync();
        saved.DockerNetworkId.ShouldBe("existing");
        saved.Subnet.ShouldBe("10.240.9.0/24");
        saved.Gateway.ShouldBe("10.240.9.1");
    }

    [Test]
    public async Task RecreateNetworkAsync_WhenIdle_RemovesOldNetworkAndCreatesOneInThePool()
    {
        var network = SeedSharedNetwork("shared-net", "old", "172.21.0.0/16");
        _client.Networks.InspectNetworkAsync("old", Arg.Any<CancellationToken>())
            .Returns(new NetworkResponse { ID = "old", Containers = new Dictionary<string, EndpointResource>() });
        NoDockerNetworksExist();
        _client.Networks.CreateNetworkAsync(Arg.Any<NetworksCreateParameters>(), Arg.Any<CancellationToken>())
            .Returns(new NetworksCreateResponse { ID = "new" });

        var result = await _sut.RecreateNetworkAsync(network.Id, "legacy subnet", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _client.Networks.Received(1).DeleteNetworkAsync("old", Arg.Any<CancellationToken>());
        var saved = await _db.Networks.SingleAsync();
        saved.DockerNetworkId.ShouldBe("new");
        saved.Subnet.ShouldBe("10.240.0.0/24");
        saved.Gateway.ShouldBe("10.240.0.1");
    }

    [Test]
    public async Task RecreateNetworkAsync_WhenContainerIsAttached_RefusesAndTouchesNothing()
    {
        var network = SeedSharedNetwork("shared-net", "old", "172.21.0.0/16");
        _client.Networks.InspectNetworkAsync("old", Arg.Any<CancellationToken>())
            .Returns(new NetworkResponse { ID = "old", Containers = new Dictionary<string, EndpointResource> { { "c1", new EndpointResource() } } });

        var result = await _sut.RecreateNetworkAsync(network.Id, "legacy subnet", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Error.Docker.NetworkInUse);
        await _client.Networks.DidNotReceive().DeleteNetworkAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await _db.Networks.SingleAsync()).DockerNetworkId.ShouldBe("old");
    }

    [Test]
    public async Task RecreateNetworkAsync_WhenOnlyAStoppedContainerReferencesIt_Refuses()
    {
        var network = SeedSharedNetwork("shared-net", "old", "172.21.0.0/16");
        _client.Networks.InspectNetworkAsync("old", Arg.Any<CancellationToken>())
            .Returns(new NetworkResponse { ID = "old", Containers = new Dictionary<string, EndpointResource>() });
        _client.Containers.ListContainersAsync(Arg.Any<ContainersListParameters>(), Arg.Any<CancellationToken>())
            .Returns([new ContainerListResponse { ID = "stopped" }]);

        var result = await _sut.RecreateNetworkAsync(network.Id, "legacy subnet", CancellationToken.None);

        result.Error.ShouldBe(Error.Docker.NetworkInUse);
        await _client.Networks.DidNotReceive().DeleteNetworkAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RecreateNetworkAsync_ExternalNetwork_IsRejected()
    {
        var network = SeedSharedNetwork("ext", "x", type: NetworkType.External);

        var result = await _sut.RecreateNetworkAsync(network.Id, "legacy subnet", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        await _client.Networks.DidNotReceive().DeleteNetworkAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RecreateNetworkAsync_WhenCreateFailsAfterRemoval_LeavesRecordForTheNextPassToHeal()
    {
        var network = SeedSharedNetwork("shared-net", "old", "172.21.0.0/16");
        _client.Networks.InspectNetworkAsync("old", Arg.Any<CancellationToken>())
            .Returns(new NetworkResponse { ID = "old", Containers = new Dictionary<string, EndpointResource>() });
        NoDockerNetworksExist();
        _client.Networks.CreateNetworkAsync(Arg.Any<NetworksCreateParameters>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<NetworksCreateResponse>(new DockerApiException(System.Net.HttpStatusCode.InternalServerError, "boom")));

        var result = await _sut.RecreateNetworkAsync(network.Id, "legacy subnet", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var saved = await _db.Networks.SingleAsync();
        saved.DockerNetworkId.ShouldBeNull();
        saved.Subnet.ShouldBeNull();

        // The next pass (or deploy) sees "not provisioned" and creates it.
        _client.Networks.CreateNetworkAsync(Arg.Any<NetworksCreateParameters>(), Arg.Any<CancellationToken>())
            .Returns(new NetworksCreateResponse { ID = "new" });
        (await _sut.EnsureNetworkExistsAsync(network.Id, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await _db.Networks.SingleAsync()).DockerNetworkId.ShouldBe("new");
    }

    [Test]
    public async Task IsNetworkInUseAsync_ReflectsAttachedContainers()
    {
        var network = SeedSharedNetwork("shared-net", "n1", "10.240.1.0/24");
        _client.Networks.InspectNetworkAsync("n1", Arg.Any<CancellationToken>())
            .Returns(new NetworkResponse { ID = "n1", Containers = new Dictionary<string, EndpointResource>() });
        _client.Containers.ListContainersAsync(Arg.Any<ContainersListParameters>(), Arg.Any<CancellationToken>()).Returns([]);

        (await _sut.IsNetworkInUseAsync(network.Id, CancellationToken.None)).ShouldBeFalse();

        _client.Networks.InspectNetworkAsync("n1", Arg.Any<CancellationToken>())
            .Returns(new NetworkResponse { ID = "n1", Containers = new Dictionary<string, EndpointResource> { { "c", new EndpointResource() } } });
        (await _sut.IsNetworkInUseAsync(network.Id, CancellationToken.None)).ShouldBeTrue();
    }
}