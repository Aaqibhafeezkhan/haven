using Haven.Application.Features.Services;
using Haven.Domain.Enums;

using Shouldly;

namespace Haven.Application.Tests.Features.Services;

[Category("Unit")]
public sealed class ServiceManifestDtoTests
{
    [Test]
    public void ParseLegacyStatus_Running_KeepsServiceRunning() =>
        ServiceManifestDto.ParseLegacyStatus("Running").ShouldBe(DesiredState.Running);

    [Test]
    public void ParseLegacyStatus_IsCaseInsensitive() =>
        ServiceManifestDto.ParseLegacyStatus("running").ShouldBe(DesiredState.Running);

    [TestCase("Stopped")]
    [TestCase("Deploying")]
    [TestCase("DeploymentPending")]
    [TestCase("Unknown")]
    [TestCase("garbage")]
    [TestCase("")]
    [TestCase(null)]
    public void ParseLegacyStatus_AnythingElse_DefaultsToStopped(string? status) =>
        ServiceManifestDto.ParseLegacyStatus(status).ShouldBe(DesiredState.Stopped);
}