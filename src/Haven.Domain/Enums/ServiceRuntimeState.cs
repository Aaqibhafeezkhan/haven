namespace Haven.Domain.Enums;

/// <summary>Observed container state. Lives on the service registry entry, never on <c>Service</c>.</summary>
public enum ServiceRuntimeState
{
    NotDeployed,
    Running,
    Restarting,
    Stopped,
    Crashed,
    Unknown
}