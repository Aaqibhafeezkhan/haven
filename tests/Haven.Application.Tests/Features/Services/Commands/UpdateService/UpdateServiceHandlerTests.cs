using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Features.Services;
using Haven.Application.Features.Services.Commands.UpdateService;
using Haven.Domain;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;

using NSubstitute;

using Shouldly;

namespace Haven.Application.Tests.Features.Services.Commands.UpdateService;

[Category("Unit")]
public sealed class UpdateServiceHandlerTests
{
    private IProjectRepository _projectRepository;
    private UpdateServiceHandler _sut;

    [SetUp]
    public void Setup()
    {
        _projectRepository = Substitute.For<IProjectRepository>();
        _sut = new UpdateServiceHandler(_projectRepository);
    }

    [Test]
    public async Task Handle_ShouldReturnFailure_WhenProjectDoesNotExist()
    {
        var command = CreateCommand();
        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>())
            .Returns((Project?)null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_ShouldReturnFailure_WhenEnvironmentDoesNotExist()
    {
        var command = CreateCommand();
        var project = Project.Create("test-project");
        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>())
            .Returns(project);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_ShouldReturnFailure_WhenServiceDoesNotExist()
    {
        var command = CreateCommand();
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("staging");
        command.EnvironmentId = environment.Id;

        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>())
            .Returns(project);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_ShouldReturnFailure_WhenServiceNameAlreadyExists()
    {
        var command = CreateCommand();
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("staging");
        command.EnvironmentId = environment.Id;
        var existingService = project.AddService(environment.Id, "api", ServiceType.DockerImage, ExposureMode.Internal);
        var serviceToUpdate = project.AddService(environment.Id, "web", ServiceType.DockerImage, ExposureMode.External);
        command.ServiceId = serviceToUpdate.Id;
        command.Name = (Optional<string>)"api";

        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>())
            .Returns(project);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_ShouldReturnFailure_WhenServiceNameAlreadyExists_CaseInsensitive()
    {
        var command = CreateCommand();
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("staging");
        command.EnvironmentId = environment.Id;
        project.AddService(environment.Id, "api", ServiceType.DockerImage, ExposureMode.Internal);
        var serviceToUpdate = project.AddService(environment.Id, "web", ServiceType.DockerImage, ExposureMode.External);
        command.ServiceId = serviceToUpdate.Id;
        command.Name = (Optional<string>)"API";

        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>())
            .Returns(project);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_ShouldUpdateServiceName()
    {
        var command = CreateCommand();
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("staging");
        command.EnvironmentId = environment.Id;
        var service = project.AddService(environment.Id, "web", ServiceType.DockerImage, ExposureMode.External);
        command.ServiceId = service.Id;
        command.Name = (Optional<string>)"web-app";

        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>())
            .Returns(project);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        service.Name.ShouldBe("web-app");
    }

    [Test]
    public async Task Handle_ShouldUpdateServiceType()
    {
        var command = CreateCommand();
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("staging");
        command.EnvironmentId = environment.Id;
        var service = project.AddService(environment.Id, "web", ServiceType.DockerImage, ExposureMode.External);
        command.ServiceId = service.Id;
        command.Type = (Optional<ServiceType>)ServiceType.DockerImage;

        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>())
            .Returns(project);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        service.Type.ShouldBe(ServiceType.DockerImage);
    }

    [Test]
    public async Task Handle_ShouldUpdateServiceExposureMode()
    {
        var command = CreateCommand();
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("staging");
        command.EnvironmentId = environment.Id;
        var service = project.AddService(environment.Id, "web", ServiceType.DockerImage, ExposureMode.External);
        command.ServiceId = service.Id;
        command.ExposureMode = (Optional<ExposureMode>)ExposureMode.Internal;

        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>())
            .Returns(project);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        service.ExposureMode.ShouldBe(ExposureMode.Internal);
    }

    [Test]
    public async Task Handle_ShouldReturnServiceId_OnSuccess()
    {
        var command = CreateCommand();
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("staging");
        command.EnvironmentId = environment.Id;
        var service = project.AddService(environment.Id, "web", ServiceType.DockerImage, ExposureMode.External);
        command.ServiceId = service.Id;

        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>())
            .Returns(project);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(service.Id);
    }

    [Test]
    public async Task Handle_ShouldKeepExistingResourceLimits_WhenResourceLimitsOmitted()
    {
        var (command, service) = CreateCommandForServiceWithLimits();
        command.DockerConfig = new DockerConfig { Image = "nginx:1.27" };

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var config = (DockerConfig)service.SourceConfig!;
        config.Image.ShouldBe("nginx:1.27");
        config.MemoryLimit.ShouldBe(512L * 1024 * 1024);
        config.NanoCpus.ShouldBe(500_000_000);
    }

    [Test]
    public async Task Handle_ShouldApplyResourceLimits_WhenProvidedWithDockerConfig()
    {
        var (command, service) = CreateCommandForServiceWithLimits();
        command.DockerConfig = new DockerConfig { Image = "nginx:1.27" };
        command.ResourceLimits = new ServiceResourceLimits { MemoryLimit = 1024L * 1024 * 1024 };

        await _sut.Handle(command, CancellationToken.None);

        var config = (DockerConfig)service.SourceConfig!;
        config.MemoryLimit.ShouldBe(1024L * 1024 * 1024);
        config.NanoCpus.ShouldBe(0);
    }

    [Test]
    public async Task Handle_ShouldApplyResourceLimits_WhenDockerConfigOmitted()
    {
        var (command, service) = CreateCommandForServiceWithLimits();
        command.ResourceLimits = new ServiceResourceLimits { MemoryLimit = 1024L * 1024 * 1024 };

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var config = (DockerConfig)service.SourceConfig!;
        config.Image.ShouldBe("nginx:1.26");
        config.MemoryLimit.ShouldBe(1024L * 1024 * 1024);
    }

    [Test]
    public async Task Handle_ShouldClearResourceLimits_WhenAllZeroLimitsProvided()
    {
        var (command, service) = CreateCommandForServiceWithLimits();
        command.DockerConfig = new DockerConfig { Image = "nginx:1.26" };
        command.ResourceLimits = new ServiceResourceLimits();

        await _sut.Handle(command, CancellationToken.None);

        var config = (DockerConfig)service.SourceConfig!;
        config.MemoryLimit.ShouldBe(0);
        config.NanoCpus.ShouldBe(0);
    }

    private (UpdateServiceCommand Command, Service Service) CreateCommandForServiceWithLimits()
    {
        var command = CreateCommand();
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("staging");
        command.EnvironmentId = environment.Id;
        var service = project.AddService(environment.Id, "web", ServiceType.DockerImage, ExposureMode.External,
            sourceConfig: new DockerConfig { Image = "nginx:1.26", MemoryLimit = 512L * 1024 * 1024, NanoCpus = 500_000_000 });
        command.ServiceId = service.Id;
        _projectRepository.GetByIdAsync(command.ProjectId, Arg.Any<CancellationToken>()).Returns(project);
        return (command, service);
    }

    private static UpdateServiceCommand CreateCommand() => new()
    {
        ProjectId = Guid.NewGuid(),
        EnvironmentId = Guid.NewGuid(),
        ServiceId = Guid.NewGuid()
    };
}