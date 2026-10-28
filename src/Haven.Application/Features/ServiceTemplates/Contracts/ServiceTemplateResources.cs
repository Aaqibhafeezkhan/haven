using Haven.Application.Features.Services;

namespace Haven.Application.Features.ServiceTemplates.Contracts;

/// <summary>
/// Default resource limits a template can ship with, in author-friendly units. A missing or zero
/// value means "not set". Users can still change these when creating a service from the template.
/// </summary>
public class ServiceTemplateResources
{
    private const long BytesPerMib = 1024 * 1024;
    private const double NanoCpusPerCore = 1_000_000_000;

    public double? MemoryLimitMib { get; set; }

    /// <summary>Total memory plus swap in MiB; -1 allows unlimited swap.</summary>
    public double? MemorySwapMib { get; set; }

    public double? MemoryReservationMib { get; set; }
    public double? Cpus { get; set; }

    public ServiceResourceLimits ToResourceLimits() => new()
    {
        MemoryLimit = ToBytes(MemoryLimitMib),
        // -1 means "unlimited swap" to Docker and must not be scaled to bytes.
        MemorySwap = MemorySwapMib == -1 ? -1 : ToBytes(MemorySwapMib),
        MemoryReservation = ToBytes(MemoryReservationMib),
        NanoCpus = Scale(Cpus, NanoCpusPerCore)
    };

    private static long ToBytes(double? mib) => Scale(mib, BytesPerMib);

    /// <summary>
    /// Converts an author-facing value to its stored unit. Non-finite or out-of-range values become
    /// <see cref="long.MinValue"/> so the limits validator rejects them, instead of wrapping to an
    /// unrelated value that would pass validation.
    /// </summary>
    private static long Scale(double? value, double scale)
    {
        var scaled = Math.Round((value ?? 0) * scale);
        return double.IsFinite(scaled) && scaled is >= long.MinValue and < long.MaxValue
            ? (long)scaled
            : long.MinValue;
    }
}
