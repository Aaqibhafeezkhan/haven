using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Features.Services;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;
using Haven.Infrastructure.Deployment;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Haven.Infrastructure.Tests.Deployment;

[Category("Unit")]
public sealed class RepositoryManifestSyncServiceTests
{
    private const string ValidYaml = "type: DockerImage\nexposureMode: Internal\n";

    private RepositoryManifestSyncService _sut = null!;
    private IGitService _gitService = null!;
    private IManifestParser<ServiceManifestDto> _parser = null!;

    [SetUp]
    public void Setup()
    {
        _gitService = Substitute.For<IGitService>();
        _parser = Substitute.For<IManifestParser<ServiceManifestDto>>();
        _sut = new RepositoryManifestSyncService(_gitService, _parser, Substitute.For<ILogger<RepositoryManifestSyncService>>());
    }

    [Test]
    public async Task SyncAsync_WhenServiceHasNoBinding_ShouldReturnFalseWithoutReadingGit()
    {
        var service = CreateService(bound: false);

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
        await _gitService.DidNotReceive().ReadBoundFileAsync(Arg.Any<RepositoryBinding>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SyncAsync_WhenReadingFileFails_ShouldReturnGitError()
    {
        var service = CreateService();
        GivenFile(service, Result<string>.Failure(Error.NotFound));

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Error.NotFound);
        await _parser.DidNotReceive().ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SyncAsync_WhenManifestOmitsRequiredKeys_ShouldReturnValidationErrorListingThem()
    {
        var service = CreateService();
        GivenFile(service, "image: foo\n");

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldContain("type");
        result.Error.Message.ShouldContain("exposureMode");
        await _parser.DidNotReceive().ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SyncAsync_WhenOnlyOneRequiredKeyIsMissing_ShouldNameOnlyThatKey()
    {
        var service = CreateService();
        GivenFile(service, "type: DockerImage\n");

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldContain("exposureMode");
        result.Error.Message.ShouldNotContain("type,");
    }

    [Test]
    public async Task SyncAsync_WhenRequiredKeysHaveDifferentCasing_ShouldAcceptThem()
    {
        var service = CreateService();
        GivenFile(service, "Type: DockerImage\nExposureMode: Internal\n");
        GivenParsed(CreateManifest(image: "myapp:latest"));

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task SyncAsync_WhenYamlIsMalformed_ShouldReturnValidationError()
    {
        var service = CreateService();
        GivenFile(service, "type: [unclosed\n  : :");

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldContain("Invalid repository manifest YAML");
    }

    [Test]
    public async Task SyncAsync_WhenParserThrows_ShouldReturnValidationError()
    {
        var service = CreateService();
        GivenFile(service, ValidYaml);
        _parser.ParseAsync(ValidYaml, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldContain("boom");
    }

    [Test]
    public async Task SyncAsync_WhenParserIsCancelled_ShouldPropagateCancellation()
    {
        var service = CreateService();
        GivenFile(service, ValidYaml);
        _parser.ParseAsync(ValidYaml, Arg.Any<CancellationToken>()).ThrowsAsync<OperationCanceledException>();

        await Should.ThrowAsync<OperationCanceledException>(() => _sut.SyncAsync(service, CancellationToken.None));
    }

    [Test]
    public async Task SyncAsync_WhenManifestChangesService_ShouldReturnTrueAndApplyIt()
    {
        var service = CreateService();
        GivenFile(service, ValidYaml);
        GivenParsed(CreateManifest(image: "myapp:v2", exposureMode: ExposureMode.External));

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        service.ExposureMode.ShouldBe(ExposureMode.External);
        ((DockerConfig)service.SourceConfig!).Image.ShouldBe("myapp:v2");
    }

    [Test]
    public async Task SyncAsync_WhenManifestMatchesService_ShouldReturnFalse()
    {
        var service = CreateService();
        GivenFile(service, ValidYaml);
        GivenParsed(CreateManifest(image: "myapp:latest"));

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Test]
    public async Task SyncAsync_WhenManifestDeclaresCustomActions_ShouldIgnoreThemAndStillSucceed()
    {
        var service = CreateService();
        GivenFile(service, ValidYaml);
        var manifest = CreateManifest(image: "myapp:latest");
        manifest.CustomActions = [new CustomActionManifest()];
        GivenParsed(manifest);

        var result = await _sut.SyncAsync(service, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        service.CustomActions.ShouldBeEmpty();
    }

    [Test]
    public async Task SyncAsync_ShouldNotOverwriteServiceIdentity()
    {
        var service = CreateService();
        var originalId = service.Id;
        GivenFile(service, ValidYaml);
        var manifest = CreateManifest(image: "myapp:v2");
        manifest.Name = "renamed";
        manifest.Id = Guid.NewGuid();
        GivenParsed(manifest);

        await _sut.SyncAsync(service, CancellationToken.None);

        service.Id.ShouldBe(originalId);
        service.Name.ShouldBe("test-svc");
    }

    [Test]
    public async Task SyncAsync_ShouldReadFileUsingServiceBinding()
    {
        var service = CreateService();
        GivenFile(service, ValidYaml);
        GivenParsed(CreateManifest(image: "myapp:latest"));

        await _sut.SyncAsync(service, CancellationToken.None);

        await _gitService.Received(1).ReadBoundFileAsync(service.RepositoryBinding!, Arg.Any<CancellationToken>());
    }

    private void GivenFile(Service service, Result<string> file) =>
        _gitService.ReadBoundFileAsync(Arg.Any<RepositoryBinding>(), Arg.Any<CancellationToken>()).Returns(file);

    private void GivenParsed(ServiceManifestDto manifest) =>
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(manifest);

    private static ServiceManifestDto CreateManifest(string image, ExposureMode exposureMode = ExposureMode.Internal) => new()
    {
        Name = "test-svc",
        Type = ServiceType.DockerImage,
        ExposureMode = exposureMode,
        SourceConfig = new ServiceSourceConfigManifest { Type = "docker", Image = image },
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Token = "token"
    };

    private static Service CreateService(bool bound = true)
    {
        var service = Service.Create(Guid.NewGuid(), "test-svc", ServiceType.DockerImage, ExposureMode.Internal,
            sourceConfig: new DockerConfig { Image = "myapp:latest" });
        if (bound)
            service.RepositoryBinding = new RepositoryBinding { Repository = "org/repo", Branch = "main", FilePath = "haven.yaml" };
        return service;
    }
}
