using Haven.Application.Common;
using Haven.Application.Common.Messaging;
using Haven.Domain.Entities;

namespace Haven.Application.Features.Services.Queries.ListVolumesForService;

[RequirePermission(Permissions.ProjectManagement.Read)]
public sealed class ListVolumesForServiceQuery : IQuery<IReadOnlyList<ServiceVolumeDto>>
{
    public Guid ServiceId { get; set; }
    public string SortBy { get; set; } = nameof(ServiceVolume.Name).ToLowerInvariant();
    public bool SortDescending { get; set; } = false;
}