using Haven.Application.Common.Interfaces;
using Haven.Domain.Events;

using Mediator;

namespace Haven.Application.Features.Manifests;

/// <summary>
/// Desired state is the only service lifecycle state kept in manifests. Stop, restart and deploy change it
/// without going through a manifest-mutating command (deploys even do so from a background job), so these
/// events request the debounced manifest resync that keeps the file's <c>desiredState</c> current.
/// </summary>
public sealed class RequestManifestSyncOnDesiredStateChangedEventHandler(IManifestSyncTrigger trigger)
    : INotificationHandler<ServiceStoppedEvent>,
        INotificationHandler<ServiceRestartedEvent>,
        INotificationHandler<ServiceDeployedEvent>
{
    public ValueTask Handle(ServiceStoppedEvent notification, CancellationToken cancellationToken) => RequestSync();
    public ValueTask Handle(ServiceRestartedEvent notification, CancellationToken cancellationToken) => RequestSync();
    public ValueTask Handle(ServiceDeployedEvent notification, CancellationToken cancellationToken) => RequestSync();

    private ValueTask RequestSync()
    {
        trigger.RequestSync();
        return ValueTask.CompletedTask;
    }
}