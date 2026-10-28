using FluentValidation.TestHelper;

using Haven.Application.Features.Services;
using Haven.Application.Features.Sidecars.Commands.UpdateSidecar;
using Haven.Domain.ValueObjects;

using Shouldly;

namespace Haven.Application.Tests.Features.Sidecars.Commands.UpdateSidecar;

[Category("Unit")]
public sealed class UpdateSidecarValidatorTests
{
    private UpdateSidecarValidator _sut;

    [SetUp]
    public void Setup() => _sut = new UpdateSidecarValidator();

    private static UpdateSidecarCommand CreateCommand(ServiceResourceLimits limits) =>
        new()
        {
            SidecarId = Guid.NewGuid(),
            DockerConfig = new DockerConfig { Image = "traefik:v3" },
            ResourceLimits = limits
        };

    [Test]
    public void Validate_ShouldPass_WhenResourceLimitsAreValid()
    {
        var command = CreateCommand(new ServiceResourceLimits
        {
            MemoryLimit = 512L * 1024 * 1024,
            MemoryReservation = 256L * 1024 * 1024,
            NanoCpus = 1_000_000_000
        });

        _sut.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_ShouldPass_WhenResourceLimitsOmitted()
    {
        var command = new UpdateSidecarCommand
        {
            SidecarId = Guid.NewGuid(),
            DockerConfig = new DockerConfig { Image = "traefik:v3" }
        };

        _sut.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_ShouldHaveError_WhenMemoryLimitBelowDockerMinimum()
    {
        var command = CreateCommand(new ServiceResourceLimits { MemoryLimit = 1000 });

        _sut.TestValidate(command).IsValid.ShouldBeFalse();
    }

    [Test]
    public void Validate_ShouldHaveError_WhenMemoryReservationExceedsLimit()
    {
        var command = CreateCommand(new ServiceResourceLimits
        {
            MemoryLimit = 64L * 1024 * 1024,
            MemoryReservation = 128L * 1024 * 1024
        });

        _sut.TestValidate(command).IsValid.ShouldBeFalse();
    }

    [Test]
    public void Validate_ShouldHaveError_WhenNanoCpusIsNegative()
    {
        var command = CreateCommand(new ServiceResourceLimits { NanoCpus = -1 });

        _sut.TestValidate(command).IsValid.ShouldBeFalse();
    }
}
