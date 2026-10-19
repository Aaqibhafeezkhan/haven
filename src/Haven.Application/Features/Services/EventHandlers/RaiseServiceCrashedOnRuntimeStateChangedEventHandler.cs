using Haven.Application.Common.Interfaces.Repositories;
using Haven.Domain.Enums;
using Haven.Domain.Events;

using Mediator;

namespace Haven.Application.Features.Services.EventHandlers;

/// <summary>
/// Turns a transition to <c>Crashed</c> into an alertable <see cref="ServiceCrashedEvent"/>, but only when the
/// operator wants the service running. An intentional stop is recorded as <c>Stopped</c>, so it never alerts.
/// </summary>
public sealed class RaiseServiceCrashedOnRuntimeStateChangedEventHandler(
    IServiceRepository serviceRepository,
    IMediator mediator) : INotificationHandler<ServiceRuntimeStateChangedEvent>
{
    public async ValueTask Handle(ServiceRuntimeStateChangedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.OwnerKind != OwnerKind.Service || notification.Next != ServiceRuntimeState.Crashed)
            return;

        var service = await serviceRepository.GetByIdAsync(notification.OwnerId, cancellationToken);
        if (service is null || service.DesiredState != DesiredState.Running)
            return;

        await mediator.Publish(new ServiceCrashedEvent(service.Id, service.Name), cancellationToken);
    }
}