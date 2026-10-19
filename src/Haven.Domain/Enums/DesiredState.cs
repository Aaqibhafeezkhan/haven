namespace Haven.Domain.Enums;

/// <summary>Operator intent for a service. Persisted on <c>Service</c> and in manifests.</summary>
public enum DesiredState
{
    Running,
    Stopped
}