using Haven.Application.Common;
using Haven.Application.Common.Messaging;
using Haven.Domain;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;

namespace Haven.Application.Features.Services.Commands.UpdateService;

[RequirePermission(Permissions.ProjectManagement.Create)]
public sealed class UpdateServiceCommand : ICommand<Guid>, IMutatesManifestState
{
    public Guid ProjectId { get; set; }
    public Guid EnvironmentId { get; set; }
    public Guid ServiceId { get; set; }
    public Optional<string> Name { get; set; }
    public Optional<string> Alias { get; set; }
    public Optional<ServiceType> Type { get; set; }
    public Optional<ExposureMode> ExposureMode { get; set; }
    public Optional<DockerConfig?> DockerConfig { get; set; }
    public Optional<DockerfileConfig?> DockerfileConfig { get; set; }

    /// <summary>
    /// Memory and CPU limits for the service. The limit fields on <see cref="DockerConfig"/> and
    /// <see cref="DockerfileConfig"/> are ignored: when this is omitted the service keeps its current limits,
    /// so editing the image or ports does not clear them. Send an all-zero value to remove the limits.
    /// </summary>
    public Optional<ServiceResourceLimits?> ResourceLimits { get; set; }

    public Optional<ServiceSourceConfig?> ResolveSourceConfig()
    {
        if (!Type.HasValue && !DockerConfig.HasValue && !DockerfileConfig.HasValue)
            return default;

        if (DockerfileConfig.HasValue)
            return (Optional<ServiceSourceConfig?>)DockerfileConfig.Value;

        if (DockerConfig.HasValue)
            return (Optional<ServiceSourceConfig?>)DockerConfig.Value;

        return default;
    }
}