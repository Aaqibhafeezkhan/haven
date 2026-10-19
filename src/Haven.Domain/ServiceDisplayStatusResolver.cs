using Haven.Domain.Enums;

namespace Haven.Domain;

/// <summary>
/// Derives the single status shown to users from the three independent state sources:
/// the in-flight deployment, the observed runtime state and the observed health.
/// </summary>
public static class ServiceDisplayStatusResolver
{
    public static ServiceDisplayStatus Resolve(
        DeploymentStatus? activeDeployment,
        ServiceRuntimeState? runtimeState,
        ServiceHealth? health)
    {
        if (activeDeployment is { } deployment && !deployment.IsTerminal)
            return deployment == DeploymentStatus.Starting ? ServiceDisplayStatus.Starting : ServiceDisplayStatus.Deploying;

        return runtimeState switch
        {
            null or ServiceRuntimeState.NotDeployed => ServiceDisplayStatus.NotDeployed,
            ServiceRuntimeState.Running => health == ServiceHealth.Unhealthy ? ServiceDisplayStatus.Degraded : ServiceDisplayStatus.Running,
            ServiceRuntimeState.Restarting => ServiceDisplayStatus.Starting,
            ServiceRuntimeState.Stopped => ServiceDisplayStatus.Stopped,
            ServiceRuntimeState.Crashed => ServiceDisplayStatus.Crashed,
            _ => ServiceDisplayStatus.Unknown
        };
    }
}
