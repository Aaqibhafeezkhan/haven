using FluentValidation;

using Haven.Domain.ValueObjects;

namespace Haven.Application.Features.Services;

/// <summary>
/// Validates the memory and CPU limits of a <see cref="DockerBasedSourceConfig"/> against the
/// constraints Docker enforces when creating a container, so an invalid combination is rejected
/// when saved instead of failing at deploy time.
/// </summary>
public sealed class ResourceLimitsValidator : AbstractValidator<DockerBasedSourceConfig>
{
    /// <summary>Docker refuses to create a container with a memory limit below 6 MiB.</summary>
    public const long MinMemoryLimitBytes = 6L * 1024 * 1024;

    /// <summary>Docker's minimum CPU quota, equal to 0.001 cores.</summary>
    public const long MinNanoCpus = 1_000_000;

    public ResourceLimitsValidator()
    {
        RuleFor(x => x.MemoryLimit)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Memory limit cannot be negative.");

        RuleFor(x => x.MemoryLimit)
            .GreaterThanOrEqualTo(MinMemoryLimitBytes)
            .When(x => x.MemoryLimit > 0)
            .WithMessage("Memory limit must be at least 6 MiB.");

        RuleFor(x => x.MemoryReservation)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Memory reservation cannot be negative.");

        RuleFor(x => x.MemoryReservation)
            .LessThanOrEqualTo(x => x.MemoryLimit)
            .When(x => x.MemoryReservation > 0 && x.MemoryLimit > 0)
            .WithMessage("Memory reservation cannot exceed the memory limit.");

        RuleFor(x => x.MemorySwap)
            .GreaterThanOrEqualTo(-1)
            .WithMessage("Memory swap must be -1 (unlimited), 0 (default) or a positive number of bytes.");

        RuleFor(x => x.MemorySwap)
            .Must(_ => false)
            .When(x => x.MemorySwap > 0 && x.MemoryLimit <= 0)
            .WithMessage("A memory limit is required when setting a memory swap limit.");

        RuleFor(x => x.MemorySwap)
            .GreaterThanOrEqualTo(x => x.MemoryLimit)
            .When(x => x.MemorySwap > 0 && x.MemoryLimit > 0)
            .WithMessage("Memory swap (memory + swap) must be at least the memory limit.");

        RuleFor(x => x.NanoCpus)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CPU limit cannot be negative.");

        RuleFor(x => x.NanoCpus)
            .GreaterThanOrEqualTo(MinNanoCpus)
            .When(x => x.NanoCpus > 0)
            .WithMessage("CPU limit must be at least 0.001 cores.");
    }
}
