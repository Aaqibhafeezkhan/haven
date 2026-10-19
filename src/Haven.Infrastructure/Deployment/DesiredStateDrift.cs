using Haven.Domain.Enums;

namespace Haven.Infrastructure.Deployment;

public enum DriftAction
{
    None,
    Start,
    Stop
}

/// <summary>Decides what, if anything, converges a container's observed runtime state toward its desired state.</summary>
public static class DesiredStateDrift
{
    /// <summary>How long a runtime state must have been stable before it is treated as drift, so the reconciler never races a transition that is still settling.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromMinutes(2);

    public static DriftAction Decide(
        DesiredState desired,
        ServiceRuntimeState runtime,
        bool deploymentActive,
        DateTime stateChangedAt,
        DateTime now)
    {
        if (deploymentActive || now - stateChangedAt < SettleTime)
            return DriftAction.None;

        return (desired, runtime) switch
        {
            // Wanted up but down. NotDeployed is excluded: there is nothing to start until it has been deployed once.
            (DesiredState.Running, ServiceRuntimeState.Stopped or ServiceRuntimeState.Crashed) => DriftAction.Start,

            // Wanted down but up.
            (DesiredState.Stopped, ServiceRuntimeState.Running) => DriftAction.Stop,

            // Restarting and Unknown are in flux or unobserved; the Docker events and the startup sync settle them.
            _ => DriftAction.None
        };
    }
}
