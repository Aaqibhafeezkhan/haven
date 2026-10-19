using Haven.Domain.Enums;

namespace Haven.Infrastructure.Deployment.Events;

/// <summary>Pure mapping from a Docker stop-like signal to the runtime state to record.</summary>
public static class ServiceRuntimeStateMapper
{
    /// <summary>
    /// A container that goes away is <c>Stopped</c> when the operator wants it stopped or it exited cleanly (exit code 0);
    /// otherwise it is whatever the signal implies, normally <c>Crashed</c> for died, killed and out-of-memory.
    /// </summary>
    public static ServiceRuntimeState ForStopSignal(DesiredState desiredState, ServiceRuntimeState unexpectedState, int? exitCode = null)
    {
        if (desiredState == DesiredState.Stopped || exitCode == 0)
            return ServiceRuntimeState.Stopped;

        return unexpectedState;
    }
}
