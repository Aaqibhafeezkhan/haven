using Haven.Domain.Enums;

namespace Haven.Domain.Events;

public sealed record ServiceHealthChangedEvent(
    Guid EntryId,
    OwnerKind OwnerKind,
    Guid OwnerId,
    ServiceHealth Previous,
    ServiceHealth Next) : DomainEvent
{
    public override string ToMessage() => $"{OwnerKind} {OwnerId} health changed from {Previous} to {Next}";
}
