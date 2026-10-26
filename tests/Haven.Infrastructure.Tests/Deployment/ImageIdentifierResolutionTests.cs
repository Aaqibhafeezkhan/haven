using Docker.DotNet.Models;

using Haven.Domain.Enums;
using Haven.Infrastructure.Deployment.Docker;

using Shouldly;

namespace Haven.Infrastructure.Tests.Deployment;

[Category("Unit")]
public sealed class ImageIdentifierResolutionTests
{
    [Test]
    public void PrefersRepoDigestOfPulledRepository()
    {
        var inspect = new ImageInspectResponse
        {
            ID = "sha256:local",
            RepoDigests = ["other/img@sha256:wrong", "nginx@sha256:right"]
        };

        var (type, identifier) = DockerContainerDeployService.ResolveImageIdentifier("nginx:latest", inspect);

        type.ShouldBe(DeploymentIdentifierType.ImageDigest);
        identifier.ShouldBe("sha256:right");
    }

    [Test]
    public void HandlesRegistryWithPortInImageName()
    {
        var inspect = new ImageInspectResponse
        {
            ID = "sha256:local",
            RepoDigests = ["registry.local:5000/app@sha256:abc"]
        };

        var (type, identifier) = DockerContainerDeployService.ResolveImageIdentifier("registry.local:5000/app", inspect);

        type.ShouldBe(DeploymentIdentifierType.ImageDigest);
        identifier.ShouldBe("sha256:abc");
    }

    [Test]
    public void FallsBackToImageId_WhenNoRepoDigests()
    {
        var inspect = new ImageInspectResponse { ID = "sha256:local", RepoDigests = [] };

        var (type, identifier) = DockerContainerDeployService.ResolveImageIdentifier("local/img", inspect);

        type.ShouldBe(DeploymentIdentifierType.ImageId);
        identifier.ShouldBe("sha256:local");
    }
}