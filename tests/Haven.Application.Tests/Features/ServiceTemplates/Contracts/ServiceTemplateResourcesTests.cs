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
}
