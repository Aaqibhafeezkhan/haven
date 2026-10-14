using Haven.Application.Mappers;
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
        var deserialized = manifest.ToDomain();

        config.ShouldBeEquivalentTo(deserialized);
    }

    [Test]
    public void DockerfileConfig_HasFullPropertyMapping()
    {
        var config = DataGen.GenerateRandomizedObject(new DockerfileConfig());
        var manifest = config.ToManifest();
        var deserialized = manifest.ToDomain();

        config.ShouldBeEquivalentTo(deserialized);
    }
}