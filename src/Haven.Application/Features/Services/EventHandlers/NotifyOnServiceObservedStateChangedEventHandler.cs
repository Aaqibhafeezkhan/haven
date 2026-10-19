using Haven.Application.Common.Interfaces.Hubs;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Domain;
using Haven.Domain.Enums;
using Haven.Domain.Events;

using Mediator;

namespace Haven.Application.Features.Services.EventHandlers;

/// <summary>
/// Pushes the recomputed display status to subscribed clients whenever a service's observed runtime
/// state or health changes. The display status is derived from the event payload plus any in-flight
/// deployment, so it doesn't depend on whether the registry entry change has been committed yet.
/// </summary>
public sealed class NotifyOnServiceObservedStateChangedEventHandler(
    IServiceRepository serviceRepository,
    IDeploymentRepository deploymentRepository,
    IServiceStatusNotifier notifier)
    : INotificationHandler<ServiceRuntimeStateChangedEvent>, INotificationHandler<ServiceHealthChangedEvent>
{
    public ValueTask Handle(ServiceRuntimeStateChangedEvent notification, CancellationToken cancellationToken) =>
        NotifyAsync(notification.OwnerKind, notification.OwnerId, notification.Next, ServiceHealth.Unknown, cancellationToken);

    public ValueTask Handle(ServiceHealthChangedEvent notification, CancellationToken cancellationToken) =>
        NotifyAsync(notification.OwnerKind, notification.OwnerId, ServiceRuntimeState.Running, notification.Next, cancellationToken);

    private async ValueTask NotifyAsync(OwnerKind ownerKind, Guid ownerId, ServiceRuntimeState runtimeState, ServiceHealth health, CancellationToken ct)
    {
        if (ownerKind != OwnerKind.Service) return;

        var service = await serviceRepository.GetByIdAsync(ownerId, ct);
        if (service is null) return;

        var active = await deploymentRepository.GetActiveStatusesAsync([ownerId], ct);
        var status = ServiceDisplayStatusResolver.Resolve(
            active.TryGetValue(ownerId, out var deployment) ? deployment : null,
            runtimeState,
            health);

        await notifier.NotifyStatusChangedAsync(ownerId, service.Name, status, ct);
    }
}