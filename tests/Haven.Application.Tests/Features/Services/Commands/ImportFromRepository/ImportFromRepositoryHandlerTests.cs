using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Features.Services;
using Haven.Application.Features.Services.Commands.ImportFromRepository;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;

using NSubstitute;

using Shouldly;

using Environment = Haven.Domain.Aggregates.Environment;

namespace Haven.Application.Tests.Features.Services.Commands.ImportFromRepository;

[Category("Unit")]
public sealed class ImportFromRepositoryHandlerTests
{
    private const string Yaml = "name: web\ntype: DockerImage\nexposureMode: Internal";

    private IGitService _gitService;
    private IManifestParser<ServiceManifestDto> _parser;
    private IEnvironmentRepository _environmentRepository;
    private IServiceRepository _serviceRepository;
    private ImportFromRepositoryHandler _sut;

    [SetUp]
    public void Setup()
    {
        _gitService = Substitute.For<IGitService>();
        _parser = Substitute.For<IManifestParser<ServiceManifestDto>>();
        _environmentRepository = Substitute.For<IEnvironmentRepository>();
        _serviceRepository = Substitute.For<IServiceRepository>();
        _serviceRepository.CanCreateAsync(Arg.Any<Service>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _gitService.ReadBoundFileAsync(Arg.Any<RepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(Yaml);
        _sut = new ImportFromRepositoryHandler(_gitService, _parser, _environmentRepository, _serviceRepository);
    }

    [Test]
    public async Task Handle_ShouldReturnNotFound_WhenEnvironmentDoesNotExist()
    {
        var command = CreateCommand();
        _environmentRepository.GetByIdAsync(command.EnvironmentId, Arg.Any<CancellationToken>())
            .Returns((Environment?)null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Error.Code.ShouldBe("NOT_FOUND");
        await _gitService.DidNotReceive().ReadBoundFileAsync(Arg.Any<RepositoryBinding>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnGitError_AndNotAdd_WhenFileCannotBeRead()
    {
        var command = CreateCommand();
        SetupEnvironment(command);
        _gitService.ReadBoundFileAsync(Arg.Any<RepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(Error.Failed);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        await _serviceRepository.DidNotReceive().AddAsync(Arg.Any<Service>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldCreateServiceBoundToTheRepository()
    {
        var command = CreateCommand();
        SetupEnvironment(command);
        _parser.ParseAsync(Yaml, Arg.Any<CancellationToken>()).Returns(CreateManifest());
        Service? saved = null;
        await _serviceRepository.AddAsync(Arg.Do<Service>(s => saved = s), Arg.Any<CancellationToken>());

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved.Id.ShouldBe(result.Value);
        saved.RepositoryBinding.ShouldNotBeNull();
        saved.RepositoryBinding.Repository.ShouldBe("https://github.com/acme/infra.git");
        saved.RepositoryBinding.Branch.ShouldBe("main");
        saved.RepositoryBinding.FilePath.ShouldBe("deploy/web.yaml");
        saved.RepositoryBinding.GitCredentialId.ShouldBe(command.GitCredentialId);
    }

    [Test]
    public async Task Handle_ShouldReadTheFileUsingTheCommandBinding()
    {
        var command = CreateCommand();
        SetupEnvironment(command);
        _parser.ParseAsync(Yaml, Arg.Any<CancellationToken>()).Returns(CreateManifest());

        await _sut.Handle(command, CancellationToken.None);

        await _gitService.Received(1).ReadBoundFileAsync(
            Arg.Is<RepositoryBinding>(b => b.Repository == command.Repository.Trim() && b.Branch == command.Branch.Trim()),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldIgnoreIdentitySecretsAndIntentFromTheManifest()
    {
        var command = CreateCommand();
        SetupEnvironment(command);
        var manifest = CreateManifest();
        var manifestId = manifest.Id = Guid.NewGuid();
        manifest.DesiredState = DesiredState.Running;
        manifest.SourceConfig = new ServiceSourceConfigManifest { Type = "dockerfile", Repository = "r", Branch = "b", GitCredentialId = Guid.NewGuid() };
        _parser.ParseAsync(Yaml, Arg.Any<CancellationToken>()).Returns(manifest);
        Service? saved = null;
        await _serviceRepository.AddAsync(Arg.Do<Service>(s => saved = s), Arg.Any<CancellationToken>());

        await _sut.Handle(command, CancellationToken.None);

        saved.ShouldNotBeNull();
        saved.Id.ShouldNotBe(manifestId);
        saved.Token.ShouldNotBe("original-token");
        saved.DesiredState.ShouldBe(DesiredState.Stopped);
        saved.GitCredentialId.ShouldBe(command.GitCredentialId);
    }

    [Test]
    public async Task Handle_ShouldReturnValidation_AndNotAdd_WhenManifestIsInvalid()
    {
        var command = CreateCommand();
        SetupEnvironment(command);
        _parser.ParseAsync(Yaml, Arg.Any<CancellationToken>()).Returns<ServiceManifestDto>(_ => throw new InvalidOperationException("bad yaml"));

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Error.Code.ShouldBe("VALIDATION");
        await _serviceRepository.DidNotReceive().AddAsync(Arg.Any<Service>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnValidation_AndNotAdd_WhenManifestOmitsRequiredKeys()
    {
        var command = CreateCommand();
        SetupEnvironment(command);
        _gitService.ReadBoundFileAsync(Arg.Any<RepositoryBinding>(), Arg.Any<CancellationToken>()).Returns("name: web");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Error.Code.ShouldBe("VALIDATION");
        result.Error.Message.ShouldContain("type");
        await _serviceRepository.DidNotReceive().AddAsync(Arg.Any<Service>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnValidation_AndNotAdd_WhenManifestHasNoName()
    {
        var command = CreateCommand();
        SetupEnvironment(command);
        var manifest = CreateManifest();
        manifest.Name = "  ";
        _parser.ParseAsync(Yaml, Arg.Any<CancellationToken>()).Returns(manifest);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Error.Code.ShouldBe("VALIDATION");
        await _serviceRepository.DidNotReceive().AddAsync(Arg.Any<Service>(), Arg.Any<CancellationToken>());
    }

    private Environment SetupEnvironment(ImportFromRepositoryCommand command)
    {
        var environment = Environment.Create(Guid.NewGuid(), "staging");
        command.EnvironmentId = environment.Id;
        _environmentRepository.GetByIdAsync(environment.Id, Arg.Any<CancellationToken>()).Returns(environment);
        return environment;
    }

    private static ImportFromRepositoryCommand CreateCommand() => new()
    {
        EnvironmentId = Guid.NewGuid(),
        GitCredentialId = Guid.NewGuid(),
        Repository = "https://github.com/acme/infra.git",
        Branch = "main",
        FilePath = "deploy/web.yaml",
    };

    private static ServiceManifestDto CreateManifest() => new()
    {
        Name = "web",
        Type = ServiceType.DockerImage,
        ExposureMode = ExposureMode.Internal,
        DesiredState = DesiredState.Stopped,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Token = "original-token",
        SourceConfig = new ServiceSourceConfigManifest { Type = "docker", Image = "nginx" },
    };
}