using Haven.Application.Features.Services;
using Haven.Application.Features.ServiceTemplates.Contracts;

using Shouldly;

namespace Haven.Application.Tests.Features.ServiceTemplates.Contracts;

[Category("Unit")]
public sealed class ServiceTemplateResourcesTests
{
    private const long Mib = 1024 * 1024;

    [Test]
    public void ToResourceLimits_ShouldConvertMibToBytesAndCoresToNanoCpus()
    {
        var resources = new ServiceTemplateResources
        {
            MemoryLimitMib = 512,
            MemorySwapMib = 1024,
            MemoryReservationMib = 256,
            Cpus = 1.5
        };

        var limits = resources.ToResourceLimits();

        limits.MemoryLimit.ShouldBe(512 * Mib);
        limits.MemorySwap.ShouldBe(1024 * Mib);
        limits.MemoryReservation.ShouldBe(256 * Mib);
        limits.NanoCpus.ShouldBe(1_500_000_000);
    }

    [Test]
    public void ToResourceLimits_ShouldTreatMissingValuesAsUnset()
    {
        var limits = new ServiceTemplateResources().ToResourceLimits();

        limits.MemoryLimit.ShouldBe(0);
        limits.MemorySwap.ShouldBe(0);
        limits.MemoryReservation.ShouldBe(0);
        limits.NanoCpus.ShouldBe(0);
    }

    [Test]
    public void ToResourceLimits_ShouldKeepUnlimitedSwapAsMinusOne()
    {
        var limits = new ServiceTemplateResources { MemoryLimitMib = 512, MemorySwapMib = -1 }.ToResourceLimits();

        limits.MemorySwap.ShouldBe(-1);
    }

    [TestCase(1e15)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NaN)]
    public void ToResourceLimits_ShouldProduceValuesTheValidatorRejects_WhenValueDoesNotFit(double mib)
    {
        var limits = new ServiceTemplateResources { MemoryLimitMib = mib, Cpus = mib }.ToResourceLimits();

        limits.MemoryLimit.ShouldBeLessThan(0);
        limits.NanoCpus.ShouldBeLessThan(0);
        new ResourceLimitsValidator().Validate(limits.ToConfig()).IsValid.ShouldBeFalse();
    }

    [Test]
    public void ToResourceLimits_ShouldProduceValuesTheValidatorRejects_WhenSwapIsNegativeButNotMinusOne()
    {
        var limits = new ServiceTemplateResources { MemoryLimitMib = 512, MemorySwapMib = -2 }.ToResourceLimits();

        new ResourceLimitsValidator().Validate(limits.ToConfig()).IsValid.ShouldBeFalse();
    }
}
