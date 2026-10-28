using FluentValidation.TestHelper;

using Haven.Application.Features.Services;
using Haven.Domain.ValueObjects;

namespace Haven.Application.Tests.Features.Services;

[Category("Unit")]
public sealed class ResourceLimitsValidatorTests
{
    private const long Mib = 1024 * 1024;

    private ResourceLimitsValidator _sut = null!;

    [SetUp]
    public void Setup() => _sut = new ResourceLimitsValidator();

    [Test]
    public void Validate_ShouldPass_WhenNoLimitsAreSet()
    {
        var result = _sut.TestValidate(new DockerConfig());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_ShouldPass_WhenAllLimitsAreValid()
    {
        var config = new DockerConfig
        {
            MemoryLimit = 512 * Mib,
            MemorySwap = 1024 * Mib,
            MemoryReservation = 256 * Mib,
            NanoCpus = 1_500_000_000
        };

        var result = _sut.TestValidate(config);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_ShouldHaveError_WhenMemoryLimitIsNegative()
    {
        var result = _sut.TestValidate(new DockerConfig { MemoryLimit = -1 });

        result.ShouldHaveValidationErrorFor(x => x.MemoryLimit);
    }

    [Test]
    public void Validate_ShouldHaveError_WhenMemoryLimitIsBelowMinimum()
    {
        var result = _sut.TestValidate(new DockerConfig { MemoryLimit = 5 * Mib });

        result.ShouldHaveValidationErrorFor(x => x.MemoryLimit);
    }

    [Test]
    public void Validate_ShouldPass_WhenMemoryLimitIsExactlyMinimum()
    {
        var result = _sut.TestValidate(new DockerConfig { MemoryLimit = ResourceLimitsValidator.MinMemoryLimitBytes });

        result.ShouldNotHaveValidationErrorFor(x => x.MemoryLimit);
    }

    [Test]
    public void Validate_ShouldHaveError_WhenMemoryReservationIsNegative()
    {
        var result = _sut.TestValidate(new DockerConfig { MemoryReservation = -1 });

        result.ShouldHaveValidationErrorFor(x => x.MemoryReservation);
    }

    [Test]
    public void Validate_ShouldHaveError_WhenReservationExceedsMemoryLimit()
    {
        var config = new DockerConfig { MemoryLimit = 256 * Mib, MemoryReservation = 512 * Mib };

        var result = _sut.TestValidate(config);

        result.ShouldHaveValidationErrorFor(x => x.MemoryReservation);
    }

    [Test]
    public void Validate_ShouldPass_WhenReservationIsSetWithoutMemoryLimit()
    {
        var result = _sut.TestValidate(new DockerConfig { MemoryReservation = 256 * Mib });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_ShouldPass_WhenSwapIsUnlimited()
    {
        var config = new DockerConfig { MemoryLimit = 512 * Mib, MemorySwap = -1 };

        var result = _sut.TestValidate(config);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_ShouldHaveError_WhenSwapIsBelowMinusOne()
    {
        var result = _sut.TestValidate(new DockerConfig { MemoryLimit = 512 * Mib, MemorySwap = -2 });

        result.ShouldHaveValidationErrorFor(x => x.MemorySwap);
    }

    [Test]
    public void Validate_ShouldHaveError_WhenSwapIsSetWithoutMemoryLimit()
    {
        var result = _sut.TestValidate(new DockerConfig { MemorySwap = 1024 * Mib });

        result.ShouldHaveValidationErrorFor(x => x.MemorySwap);
    }

    [Test]
    public void Validate_ShouldHaveError_WhenSwapIsBelowMemoryLimit()
    {
        var config = new DockerConfig { MemoryLimit = 512 * Mib, MemorySwap = 256 * Mib };

        var result = _sut.TestValidate(config);

        result.ShouldHaveValidationErrorFor(x => x.MemorySwap);
    }

    [Test]
    public void Validate_ShouldPass_WhenSwapEqualsMemoryLimit()
    {
        var config = new DockerConfig { MemoryLimit = 512 * Mib, MemorySwap = 512 * Mib };

        var result = _sut.TestValidate(config);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_ShouldHaveError_WhenNanoCpusIsNegative()
    {
        var result = _sut.TestValidate(new DockerConfig { NanoCpus = -1 });

        result.ShouldHaveValidationErrorFor(x => x.NanoCpus);
    }

    [Test]
    public void Validate_ShouldHaveError_WhenNanoCpusIsBelowMinimum()
    {
        var result = _sut.TestValidate(new DockerConfig { NanoCpus = ResourceLimitsValidator.MinNanoCpus - 1 });

        result.ShouldHaveValidationErrorFor(x => x.NanoCpus);
    }

    [Test]
    public void Validate_ShouldPass_WhenNanoCpusIsExactlyMinimum()
    {
        var result = _sut.TestValidate(new DockerConfig { NanoCpus = ResourceLimitsValidator.MinNanoCpus });

        result.ShouldNotHaveValidationErrorFor(x => x.NanoCpus);
    }

    [Test]
    public void Validate_ShouldAlsoApplyToDockerfileConfig()
    {
        var result = _sut.TestValidate(new DockerfileConfig { MemoryLimit = -1 });

        result.ShouldHaveValidationErrorFor(x => x.MemoryLimit);
    }
}