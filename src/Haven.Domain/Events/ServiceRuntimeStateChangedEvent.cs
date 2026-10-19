using Haven.Domain.Enums;

namespace Haven.Domain.Events;

public sealed record ServiceRuntimeStateChangedEvent(
    Guid EntryId,
    OwnerKind OwnerKind,
    Guid OwnerId,
    ServiceRuntimeState Previous,
    ServiceRuntimeState Next) : DomainEvent
{
    public override string ToMessage() => $"{OwnerKind} {OwnerId} runtime state changed from {Previous} to {Next}";
}
