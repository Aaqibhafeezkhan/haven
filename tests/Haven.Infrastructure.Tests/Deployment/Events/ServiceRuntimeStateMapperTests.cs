using Haven.Domain.Enums;
using Haven.Infrastructure.Deployment.Events;

using Shouldly;

namespace Haven.Infrastructure.Tests.Deployment.Events;

[TestFixture]
[Category("Unit")]
public sealed class ServiceRuntimeStateMapperTests
{
    [TestCase(DesiredState.Running, 137, ServiceRuntimeState.Crashed)]
    [TestCase(DesiredState.Running, 1, ServiceRuntimeState.Crashed)]
    [TestCase(DesiredState.Running, null, ServiceRuntimeState.Crashed)]
    [TestCase(DesiredState.Running, 0, ServiceRuntimeState.Stopped)]
    [TestCase(DesiredState.Stopped, 137, ServiceRuntimeState.Stopped)]
    [TestCase(DesiredState.Stopped, 1, ServiceRuntimeState.Stopped)]
    [TestCase(DesiredState.Stopped, null, ServiceRuntimeState.Stopped)]
    [TestCase(DesiredState.Stopped, 0, ServiceRuntimeState.Stopped)]
    public void ForStopSignal_CrashSignals_AreCrashedUnlessIntentionalOrClean(DesiredState desired, int? exitCode, ServiceRuntimeState expected)
    {
        ServiceRuntimeStateMapper.ForStopSignal(desired, ServiceRuntimeState.Crashed, exitCode).ShouldBe(expected);
    }

    [TestCase(DesiredState.Running, null)]
    [TestCase(DesiredState.Running, 0)]
    [TestCase(DesiredState.Stopped, null)]
    public void ForStopSignal_PlainStopSignal_IsAlwaysStopped(DesiredState desired, int? exitCode)
    {
        ServiceRuntimeStateMapper.ForStopSignal(desired, ServiceRuntimeState.Stopped, exitCode).ShouldBe(ServiceRuntimeState.Stopped);
    }
}
