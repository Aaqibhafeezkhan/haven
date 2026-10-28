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
        NanoCpus = (long)Math.Round((Cpus ?? 0) * NanoCpusPerCore)
    };

    private static long ToBytes(double? mib) => (long)Math.Round((mib ?? 0) * BytesPerMib);
}
