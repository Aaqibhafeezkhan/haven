using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Features.Services;
using Haven.Application.Features.Sidecars.Commands.UpdateSidecar;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;

using NSubstitute;

using Shouldly;

namespace Haven.Application.Tests.Features.Sidecars.Commands.UpdateSidecar;

[Category("Unit")]
public sealed class UpdateSidecarHandlerTests
{
    private ISidecarRepository _sidecarRepository;
    private IDeploymentJobEnqueuer _deploymentJobEnqueuer;
    private UpdateSidecarHandler _sut;

    [SetUp]
    public void Setup()
    {
        _sidecarRepository = Substitute.For<ISidecarRepository>();
        _deploymentJobEnqueuer = Substitute.For<IDeploymentJobEnqueuer>();
        _sut = new UpdateSidecarHandler(_sidecarRepository, _deploymentJobEnqueuer);
    }

    [Test]
    public async Task Handle_ShouldReturnFailure_WhenSidecarDoesNotExist()
    {
        var command = new UpdateSidecarCommand { SidecarId = Guid.NewGuid() };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns((Sidecar?)null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_ShouldUpdateSourceConfig_ButNotEnqueueDeployment_WhenSidecarDisabled()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik,
            sourceConfig: new DockerConfig { Image = "traefik:v3.0" });
        var newConfig = new DockerConfig { Image = "traefik:v3.0", CommandArgs = ["--api.dashboard=true"] };
        var command = new UpdateSidecarCommand { SidecarId = sidecar.Id, DockerConfig = newConfig };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        ((DockerConfig)sidecar.SourceConfig!).CommandArgs.ShouldContain("--api.dashboard=true");
        _deploymentJobEnqueuer.DidNotReceive().EnqueueSidecarDeployment(Arg.Any<Guid>());
    }

    [Test]
    public async Task Handle_ShouldUpdateSourceConfig_AndEnqueueDeployment_WhenSidecarEnabled()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik,
            sourceConfig: new DockerConfig { Image = "traefik:v3.0" });
        sidecar.Enable();
        var newConfig = new DockerConfig { Image = "traefik:v3.0", CommandArgs = ["--api.dashboard=true"] };
        var command = new UpdateSidecarCommand { SidecarId = sidecar.Id, DockerConfig = newConfig };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _deploymentJobEnqueuer.Received(1).EnqueueSidecarDeployment(sidecar.Id);
    }

    [Test]
    public async Task Handle_ShouldNotEnqueueDeployment_WhenNoChangesMade()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik,
            sourceConfig: new DockerConfig { Image = "traefik:v3.0" });
        sidecar.Enable();
        var command = new UpdateSidecarCommand { SidecarId = sidecar.Id };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _deploymentJobEnqueuer.DidNotReceive().EnqueueSidecarDeployment(Arg.Any<Guid>());
    }

    [Test]
    public async Task Handle_ShouldKeepExistingResourceLimits_WhenResourceLimitsOmitted()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik,
            sourceConfig: new DockerConfig { Image = "traefik:v3.0", MemoryLimit = 256L * 1024 * 1024, NanoCpus = 500_000_000 });
        var command = new UpdateSidecarCommand
        {
            SidecarId = sidecar.Id,
            DockerConfig = new DockerConfig { Image = "traefik:v3.1" }
        };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var config = (DockerConfig)sidecar.SourceConfig!;
        config.Image.ShouldBe("traefik:v3.1");
        config.MemoryLimit.ShouldBe(256L * 1024 * 1024);
        config.NanoCpus.ShouldBe(500_000_000);
    }

    [Test]
    public async Task Handle_ShouldApplyResourceLimits_WhenProvided()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik,
            sourceConfig: new DockerConfig { Image = "traefik:v3.0", MemoryLimit = 256L * 1024 * 1024 });
        var command = new UpdateSidecarCommand
        {
            SidecarId = sidecar.Id,
            DockerConfig = new DockerConfig { Image = "traefik:v3.0" },
            ResourceLimits = new ServiceResourceLimits { MemoryLimit = 512L * 1024 * 1024 }
        };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);

        await _sut.Handle(command, CancellationToken.None);

        ((DockerConfig)sidecar.SourceConfig!).MemoryLimit.ShouldBe(512L * 1024 * 1024);
    }

    [Test]
    public async Task Handle_ShouldApplyResourceLimits_WhenDockerConfigOmitted()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik,
            sourceConfig: new DockerConfig { Image = "traefik:v3.0", MemoryLimit = 256L * 1024 * 1024 });
        sidecar.Enable();
        var command = new UpdateSidecarCommand
        {
            SidecarId = sidecar.Id,
            ResourceLimits = new ServiceResourceLimits { MemoryLimit = 512L * 1024 * 1024 }
        };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var config = (DockerConfig)sidecar.SourceConfig!;
        config.Image.ShouldBe("traefik:v3.0");
        config.MemoryLimit.ShouldBe(512L * 1024 * 1024);
        _deploymentJobEnqueuer.Received(1).EnqueueSidecarDeployment(sidecar.Id);
    }

    [Test]
    public async Task Handle_ShouldClearResourceLimits_WhenAllZeroLimitsProvided()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik,
            sourceConfig: new DockerConfig { Image = "traefik:v3.0", MemoryLimit = 256L * 1024 * 1024 });
        var command = new UpdateSidecarCommand
        {
            SidecarId = sidecar.Id,
            DockerConfig = new DockerConfig { Image = "traefik:v3.0" },
            ResourceLimits = new ServiceResourceLimits()
        };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);

        await _sut.Handle(command, CancellationToken.None);

        ((DockerConfig)sidecar.SourceConfig!).MemoryLimit.ShouldBe(0);
    }
}