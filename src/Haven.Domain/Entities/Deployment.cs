using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.Exceptions;

namespace Haven.Domain.Entities;

public class Deployment : Entity
{
    public Guid ServiceId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DeploymentStatus Status { get; set; }
    public string? TriggeredBy { get; set; }
    public string LogFile { get; set; }

    public Service? Service { get; set; }

    private Deployment() { }

    public static Deployment Create(Guid serviceId, string logFile, string? triggeredBy = null)
    {
        return new Deployment
        {
            Id = Guid.NewGuid(),
            ServiceId = serviceId,
            StartedAt = DateTimeOffset.UtcNow,
            LogFile = logFile,
            TriggeredBy = triggeredBy,
            Status = DeploymentStatus.Queued
        };
    }

    public void StartBuilding()
    {
        EnsureStatus(DeploymentStatus.Building, DeploymentStatus.Queued);
        Status = DeploymentStatus.Building;
    }

    public void StartStarting()
    {
        EnsureStatus(DeploymentStatus.Starting, DeploymentStatus.Queued, DeploymentStatus.Building);
        Status = DeploymentStatus.Starting;
    }

    public void Complete()
    {
        EnsureStatus(DeploymentStatus.Succeeded, DeploymentStatus.Starting);
        FinishedAt = DateTimeOffset.UtcNow;
        Status = DeploymentStatus.Succeeded;
    }

    public void Fail()
    {
        EnsureNotTerminal(DeploymentStatus.Failed);
        FinishedAt = DateTimeOffset.UtcNow;
        Status = DeploymentStatus.Failed;
    }

    public void Cancel()
    {
        EnsureNotTerminal(DeploymentStatus.Cancelled);
        FinishedAt = DateTimeOffset.UtcNow;
        Status = DeploymentStatus.Cancelled;
    }

    private void EnsureNotTerminal(DeploymentStatus target)
    {
        if (Status.IsTerminal)
            throw new ValidationException($"Deployment is already {Status} and cannot transition to {target}.");
    }

    private void EnsureStatus(DeploymentStatus target, params DeploymentStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
            throw new ValidationException($"Deployment cannot transition from {Status} to {target}.");
    }
}
