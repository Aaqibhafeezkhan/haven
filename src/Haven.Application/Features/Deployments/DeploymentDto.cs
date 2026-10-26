using Haven.Domain;
using Haven.Domain.Enums;

namespace Haven.Application.Features.Deployments;

public sealed class DeploymentDto
{
    public Guid Id { get; init; }
    public Guid ServiceId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public DeploymentStatus Status { get; init; }
    public string? TriggeredBy { get; init; }
    public DeploymentIdentifierType? IdentifierType { get; init; }
    public string? Identifier { get; init; }
    public string? CommitMessage { get; init; }
}