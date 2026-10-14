using Haven.Domain;
using Haven.Domain.Enums;

namespace Haven.Domain.Events;

public sealed record NetworkRecreatedEvent(
    Guid NetworkId,
    string Name,
    string? PreviousSubnet,
    string? NewSubnet,
    string Reason) : DomainEvent, IScopedDomainEvent
{
    public NotificationScope PrimaryScope => NotificationScope.Global;
    public Guid PrimaryScopeId => NetworkId;

    public override string ToMessage() =>
        $"Network \"{Name}\" ({NetworkId}) was recreated ({PreviousSubnet ?? "no subnet"} -> {NewSubnet ?? "no subnet"}): {Reason}";
}
