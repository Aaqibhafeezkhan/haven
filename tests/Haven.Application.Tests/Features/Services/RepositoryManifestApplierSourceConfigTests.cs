using Haven.Application.Features.Services;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;

using Shouldly;

namespace Haven.Application.Tests.Features.Services;

[Category("Unit")]
public sealed class RepositoryManifestApplierSourceConfigTests
{
    private static Service CreateService(ServiceSourceConfig? sourceConfig) =>
        Service.Create(Guid.NewGuid(), "svc", ServiceType.Dockerfile, ExposureMode.Internal, sourceConfig: sourceConfig);

    private static ServiceManifestDto Manifest(ServiceType type) => new()
    {
        Name = "svc",
        Type = type,
        ExposureMode = ExposureMode.Internal,
        Token = string.Empty,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        SourceConfig = null
    };

    [Test]
    public void Apply_WhenManifestDropsSourceConfig_ShouldClearExistingOne()
    {
        var credential = Guid.NewGuid();
        var service = CreateService(new DockerfileConfig { Source = DockerfileSource.Git, Repository = "https://h/o/r", GitCredentialId = credential });
        var manifest = Manifest(ServiceType.DockerImage);

        var changed = RepositoryManifestApplier.Apply(service, manifest);

        changed.ShouldBeTrue();
        service.SourceConfigJson.ShouldBeNull();
        service.GitCredentialId.ShouldBeNull();
        service.Type.ShouldBe(ServiceType.DockerImage);
    }

    [Test]
    public void Apply_WhenManifestHasNoSourceConfigAndServiceHasNone_ShouldNotReportChange()
    {
        var service = CreateService(null);
        var manifest = Manifest(ServiceType.Dockerfile);

        RepositoryManifestApplier.Apply(service, manifest).ShouldBeFalse();
    }
}