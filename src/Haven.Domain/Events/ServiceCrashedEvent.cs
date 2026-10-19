using Haven.Domain.Enums;

namespace Haven.Domain.Events;

/// <summary>
/// A service that the operator wants running stopped unexpectedly. Never raised for an intentional stop.
/// </summary>
public sealed record ServiceCrashedEvent(Guid ServiceId, string Name) : DomainEvent, IScopedDomainEvent
{
    public NotificationScope PrimaryScope => NotificationScope.Service;
    public Guid PrimaryScopeId => ServiceId;
    public override string ToMessage() => $"Service \"{Name}\" ({ServiceId}) crashed unexpectedly";
}
