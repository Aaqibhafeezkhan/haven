using Haven.Application.Features.Services;
using Haven.Application.Features.Sidecars;
using Haven.Application.Mappers;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;
using Haven.Testing.Common;

using Shouldly;

namespace Haven.Application.Tests.Mappers;

[TestFixture]
[Category("Unit")]
public class SourceConfigMappingTests
{
    [Test]
    public void DockerConfig_HasFullPropertyMapping()
    {
        var config = DataGen.GenerateRandomizedObject(new DockerConfig());
        var manifest = config.ToManifest();
        var deserialized = manifest.ToDomain(ServiceType.DockerImage);

        deserialized.ShouldBeEquivalentTo(config);
    }

    [Test]
    public void DockerfileConfig_HasFullPropertyMapping()
    {
        var config = DataGen.GenerateRandomizedObject(new DockerfileConfig());
        var manifest = config.ToManifest();
        var deserialized = manifest.ToDomain(ServiceType.Dockerfile);

        config.ShouldBeEquivalentTo(deserialized);
    }

    [Test]
    public void ToDockerConfig_MapsResourceLimits()
    {
        var manifest = new ServiceSourceConfigManifest
        {
            Type = "docker", Image = "nginx", MemoryLimit = 100, MemorySwap = 200, MemoryReservation = 50, NanoCpus = 3
        };

        var config = manifest.ToDockerConfig();

        config.MemoryLimit.ShouldBe(100);
        config.MemorySwap.ShouldBe(200);
        config.MemoryReservation.ShouldBe(50);
        config.NanoCpus.ShouldBe(3);
    }

    [Test]
    public void ToDockerfileConfig_MapsResourceLimits()
    {
        var manifest = new ServiceSourceConfigManifest
        {
            Type = "dockerfile", MemoryLimit = 100, MemorySwap = 200, MemoryReservation = 50, NanoCpus = 3
        };

        var config = manifest.ToDockerfileConfig();

        config.MemoryLimit.ShouldBe(100);
        config.MemorySwap.ShouldBe(200);
        config.MemoryReservation.ShouldBe(50);
        config.NanoCpus.ShouldBe(3);
    }

    [Test]
    public void SidecarFromManifest_WithDockerType_MapsSourceConfigAndLimits()
    {
        var dto = new SidecarManifestDto
        {
            Name = "traefik",
            Kind = "Traefik",
            SourceConfig = new ServiceSourceConfigManifest { Type = "docker", Image = "traefik:v3", MemoryLimit = 100 }
        };

        var sidecar = dto.FromManifest();

        var config = sidecar.SourceConfig.ShouldBeOfType<DockerConfig>();
        config.Image.ShouldBe("traefik:v3");
        config.MemoryLimit.ShouldBe(100);
    }

    [Test]
    public void SidecarFromManifest_WithoutSourceConfig_MapsToNull()
    {
        var dto = new SidecarManifestDto { Name = "traefik", Kind = "Traefik" };

        dto.FromManifest().SourceConfig.ShouldBeNull();
    }
}
