namespace Haven.Domain.Enums;

public enum DeploymentStatus
{
    Queued,
    Building,
    Starting,
    Succeeded,
    Failed,
    Cancelled
}

public static class DeploymentStatusExtensions
{
    extension(DeploymentStatus status)
    {
        public bool IsTerminal =>
            status is DeploymentStatus.Succeeded or DeploymentStatus.Failed or DeploymentStatus.Cancelled;
    }
}