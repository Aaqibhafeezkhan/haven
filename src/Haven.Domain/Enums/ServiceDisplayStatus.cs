namespace Haven.Domain.Enums;

/// <summary>Computed for display from desired state, runtime state, health and the active deployment. Never persisted.</summary>
public enum ServiceDisplayStatus
{
    NotDeployed,
    Deploying,
    Starting,
    Running,
    Degraded,
    Stopped,
    Crashed,
    Unknown
}