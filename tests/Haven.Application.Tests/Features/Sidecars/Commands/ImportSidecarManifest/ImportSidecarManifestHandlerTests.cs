using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Features.Services;
using Haven.Application.Features.Sidecars;
using Haven.Application.Features.Sidecars.Commands.ImportSidecarManifest;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;

using NSubstitute;

using Shouldly;

namespace Haven.Application.Tests.Features.Sidecars.Commands.ImportSidecarManifest;

[Category("Unit")]
public sealed class ImportSidecarManifestHandlerTests
{
    private ISidecarRepository _sidecarRepository;
    private IManifestSerializer<Sidecar> _sidecarSerializer;
    private IManifestParser<SidecarManifestDto> _sidecarManifestParser;
    private ImportSidecarManifestHandler _sut;

    [SetUp]
    public void Setup()
    {
        _sidecarRepository = Substitute.For<ISidecarRepository>();
        _sidecarSerializer = Substitute.For<IManifestSerializer<Sidecar>>();
        _sidecarManifestParser = Substitute.For<IManifestParser<SidecarManifestDto>>();
        _sut = new ImportSidecarManifestHandler(_sidecarRepository, _sidecarSerializer, _sidecarManifestParser);
    }

    [Test]
    public async Task Handle_ShouldReturnFailure_WhenSidecarDoesNotExist()
    {
        var command = new ImportSidecarManifestCommand { SidecarId = Guid.NewGuid() };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>())
            .Returns((Sidecar?)null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_ShouldReturnFailure_WhenManifestFileIsMissing()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik);
        var command = new ImportSidecarManifestCommand { SidecarId = sidecar.Id };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);
        _sidecarSerializer.ReadManifestAsync(sidecar, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<string>(new FileNotFoundException()));

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_ShouldApplyManifest_FromDisk_WhenNoManifestYamlProvided()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik, alias: "old-alias");
        var command = new ImportSidecarManifestCommand { SidecarId = sidecar.Id };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);
        _sidecarSerializer.ReadManifestAsync(sidecar, Arg.Any<CancellationToken>()).Returns("yaml-content");
        _sidecarManifestParser.ParseAsync("yaml-content", Arg.Any<CancellationToken>())
            .Returns(new SidecarManifestDto
            {
                Name = "traefik",
                Alias = "new-alias",
                Kind = "Traefik"
            });

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        sidecar.Alias.ShouldBe("new-alias");
    }

    [Test]
    public async Task Handle_ShouldApplyManifest_FromRequestBody_WhenManifestYamlProvided()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik, alias: "old-alias");
        var command = new ImportSidecarManifestCommand { SidecarId = sidecar.Id, ManifestYaml = "pasted-yaml" };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);
        _sidecarManifestParser.ParseAsync("pasted-yaml", Arg.Any<CancellationToken>())
            .Returns(new SidecarManifestDto
            {
                Name = "traefik",
                Alias = "pasted-alias",
                Kind = "Traefik"
            });

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        sidecar.Alias.ShouldBe("pasted-alias");
        await _sidecarSerializer.DidNotReceive().ReadManifestAsync(Arg.Any<Sidecar>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldApplyDockerSourceConfigWithResourceLimits_WhenManifestHasDockerType()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik);
        var command = new ImportSidecarManifestCommand { SidecarId = sidecar.Id, ManifestYaml = "yaml" };
        _sidecarRepository.GetByIdAsync(command.SidecarId, Arg.Any<CancellationToken>()).Returns(sidecar);
        _sidecarManifestParser.ParseAsync("yaml", Arg.Any<CancellationToken>())
            .Returns(new SidecarManifestDto
            {
                Name = "traefik",
                Kind = "Traefik",
                SourceConfig = new ServiceSourceConfigManifest
                {
                    Type = "docker",
                    Image = "traefik:v3",
                    MemoryLimit = 512 * 1024 * 1024,
                    NanoCpus = 1_000_000_000
                }
            });

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var config = sidecar.SourceConfig.ShouldBeOfType<DockerConfig>();
        config.Image.ShouldBe("traefik:v3");
        config.MemoryLimit.ShouldBe(512 * 1024 * 1024);
        config.NanoCpus.ShouldBe(1_000_000_000);
    }
}
