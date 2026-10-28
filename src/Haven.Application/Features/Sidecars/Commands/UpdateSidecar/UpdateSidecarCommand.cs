using Haven.Application.Common;
using Haven.Application.Common.Messaging;
using Haven.Application.Features.Services;
using Haven.Domain;
using Haven.Domain.ValueObjects;

namespace Haven.Application.Features.Sidecars.Commands.UpdateSidecar;

[RequirePermission(Permissions.Sidecars.Manage)]
public sealed class UpdateSidecarCommand : ICommand<Guid>, IMutatesManifestState
{
    public Guid SidecarId { get; set; }
    public Optional<DockerConfig?> DockerConfig { get; set; }

    /// <summary>
    /// Memory and CPU limits for the sidecar. The limit fields on <see cref="DockerConfig"/> are ignored:
    /// when this is omitted the sidecar keeps its current limits, so editing the image or ports does not
    /// clear them. Send an all-zero value to remove the limits.
    /// </summary>
    public Optional<ServiceResourceLimits?> ResourceLimits { get; set; }
}