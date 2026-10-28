using Haven.Domain.ValueObjects;

namespace Haven.Application.Features.Services;

/// <summary>
/// Memory and CPU limits for a Docker-based service, in the same units stored on
/// <see cref="DockerBasedSourceConfig"/>: bytes and nano CPUs, where 0 means "not set".
/// </summary>
public sealed class ServiceResourceLimits
{
    public long MemoryLimit { get; set; }
    public long MemorySwap { get; set; }
    public long MemoryReservation { get; set; }
    public long NanoCpus { get; set; }

    public void ApplyTo(DockerBasedSourceConfig config)
    {
        config.MemoryLimit = MemoryLimit;
        config.MemorySwap = MemorySwap;
        config.MemoryReservation = MemoryReservation;
        config.NanoCpus = NanoCpus;
    }

    /// <summary>A throwaway config carrying only these limits, for running <see cref="ResourceLimitsValidator"/> over them.</summary>
    public DockerConfig ToConfig()
    {
        var config = new DockerConfig();
        ApplyTo(config);
        return config;
    }
}
