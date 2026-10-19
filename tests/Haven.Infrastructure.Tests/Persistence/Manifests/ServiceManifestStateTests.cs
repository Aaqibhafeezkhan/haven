using Haven.Application.Mappers;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Infrastructure.Persistence.Manifests;

using Shouldly;

using Environment = Haven.Domain.Aggregates.Environment;

namespace Haven.Infrastructure.Tests.Persistence.Manifests;

[TestFixture]
[Category("Unit")]
public sealed class ServiceManifestStateTests
{
    private static Service NewService(DesiredState desired)
    {
        var service = Service.Create(Guid.NewGuid(), "svc", ServiceType.DockerImage, ExposureMode.None);
        service.SetDesiredState(desired);
        return service;
    }

    [TestCase(DesiredState.Running, "desiredState: Running")]
    [TestCase(DesiredState.Stopped, "desiredState: Stopped")]
    public void Serialize_WritesDesiredStateAndNothingObserved(DesiredState desired, string expected)
    {
        var yaml = YamlSerializerPresets.CreateSerializer().Serialize(NewService(desired).ToManifest());

        yaml.ShouldContain(expected);
        yaml.ShouldNotContain("status:");
        yaml.ShouldNotContain("runtimeState");
        yaml.ShouldNotContain("health");
        yaml.ShouldNotContain("deployment");
    }

    [Test]
    public void Deserialize_ManifestWithoutAnyStateKey_DefaultsToStopped()
    {
        var yaml = """
            name: web
            type: DockerImage
            exposureMode: None
            createdAt: 2026-01-01T00:00:00Z
            updatedAt: 2026-01-01T00:00:00Z
            token: abc
            """;
        var dto = YamlSerializerPresets.CreateDeserializer().Deserialize<Haven.Application.Features.Services.ServiceManifestDto>(yaml);
        var environment = Environment.Create(Guid.NewGuid(), "dev");

        dto.ToEntity(environment).DesiredState.ShouldBe(DesiredState.Stopped);
    }

    [Test]
    public void Deserialize_LegacyManifestWithRunningStatus_KeepsServiceRunning()
    {
        var yaml = """
            name: web
            type: DockerImage
            exposureMode: None
            status: Running
            createdAt: 2026-01-01T00:00:00Z
            updatedAt: 2026-01-01T00:00:00Z
            token: abc
            """;
        var dto = YamlSerializerPresets.CreateDeserializer().Deserialize<Haven.Application.Features.Services.ServiceManifestDto>(yaml);
        var environment = Environment.Create(Guid.NewGuid(), "dev");

        dto.ToEntity(environment).DesiredState.ShouldBe(DesiredState.Running);
    }

    [Test]
    public void Deserialize_DesiredStateKeyWinsOverLegacyStatus()
    {
        var yaml = """
            name: web
            type: DockerImage
            exposureMode: None
            desiredState: Stopped
            status: Running
            createdAt: 2026-01-01T00:00:00Z
            updatedAt: 2026-01-01T00:00:00Z
            token: abc
            """;
        var dto = YamlSerializerPresets.CreateDeserializer().Deserialize<Haven.Application.Features.Services.ServiceManifestDto>(yaml);
        var environment = Environment.Create(Guid.NewGuid(), "dev");

        dto.ToEntity(environment).DesiredState.ShouldBe(DesiredState.Stopped);
    }

    [Test]
    public void RoundTrip_PreservesDesiredState()
    {
        var yaml = YamlSerializerPresets.CreateSerializer().Serialize(NewService(DesiredState.Running).ToManifest());
        var dto = YamlSerializerPresets.CreateDeserializer().Deserialize<Haven.Application.Features.Services.ServiceManifestDto>(yaml);

        dto.ToEntity(Environment.Create(Guid.NewGuid(), "dev")).DesiredState.ShouldBe(DesiredState.Running);
    }
}
