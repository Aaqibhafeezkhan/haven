using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Infrastructure.Deployment.Events;

using NSubstitute;

using Shouldly;

namespace Haven.Infrastructure.Tests.Deployment.Events;

[TestFixture]
[Category("Unit")]
public sealed class ServiceRuntimeStateRecorderTests
{
    private IServiceRegistry _registry = null!;
    private IDeploymentRepository _deployments = null!;
    private ServiceRuntimeStateRecorder _sut = null!;
    private ServiceRegistryEntry _entry = null!;
    private Service _service = null!;

    [SetUp]
    public void Setup()
    {
        _registry = Substitute.For<IServiceRegistry>();
        _deployments = Substitute.For<IDeploymentRepository>();
        _deployments.GetActiveStatusesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, DeploymentStatus>());
        _sut = new ServiceRuntimeStateRecorder(_registry, _deployments);

        _service = Service.Create(Guid.NewGuid(), "svc", ServiceType.DockerImage, ExposureMode.None);
        _service.SetDesiredState(DesiredState.Running);
        _entry = ServiceRegistryEntry.Create(_service.Id);
        _entry.ApplyRuntimeState(ServiceRuntimeState.Running, DateTime.UtcNow.AddMinutes(-10));
        _registry.EnsureServiceRegisteredAsync(_service.Id, Arg.Any<CancellationToken>()).Returns(_entry);
    }

    [Test]
    public async Task ApplyStop_Died_WhenDesiredRunning_RecordsCrashed()
    {
        var changed = await _sut.ApplyStopAsync(_service, ServiceRuntimeState.Crashed, exitCode: 137, CancellationToken.None);

        changed.ShouldBeTrue();
        _entry.RuntimeState.ShouldBe(ServiceRuntimeState.Crashed);
    }

    [Test]
    public async Task ApplyStop_Died_WhenDesiredStopped_RecordsStopped()
    {
        _service.SetDesiredState(DesiredState.Stopped);

        await _sut.ApplyStopAsync(_service, ServiceRuntimeState.Crashed, exitCode: 137, CancellationToken.None);

        _entry.RuntimeState.ShouldBe(ServiceRuntimeState.Stopped);
    }

    [Test]
    public async Task ApplyStop_Died_WithCleanExit_RecordsStopped()
    {
        await _sut.ApplyStopAsync(_service, ServiceRuntimeState.Crashed, exitCode: 0, CancellationToken.None);

        _entry.RuntimeState.ShouldBe(ServiceRuntimeState.Stopped);
    }

    [Test]
    public async Task ApplyStop_OutOfMemory_WithoutExitCode_RecordsCrashed()
    {
        await _sut.ApplyStopAsync(_service, ServiceRuntimeState.Crashed, exitCode: null, CancellationToken.None);

        _entry.RuntimeState.ShouldBe(ServiceRuntimeState.Crashed);
    }

    [Test]
    public async Task ApplyStop_WhileDeploymentIsActive_IsIgnored()
    {
        _deployments.GetActiveStatusesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, DeploymentStatus> { [_service.Id] = DeploymentStatus.Building });

        var changed = await _sut.ApplyStopAsync(_service, ServiceRuntimeState.Crashed, null, CancellationToken.None);

        changed.ShouldBeFalse();
        _entry.RuntimeState.ShouldBe(ServiceRuntimeState.Running);
    }

    [Test]
    public async Task ApplyStop_WhileRestarting_IsIgnored()
    {
        _entry.ApplyRuntimeState(ServiceRuntimeState.Restarting, DateTime.UtcNow);

        var changed = await _sut.ApplyStopAsync(_service, ServiceRuntimeState.Crashed, null, CancellationToken.None);

        changed.ShouldBeFalse();
        _entry.RuntimeState.ShouldBe(ServiceRuntimeState.Restarting);
    }

    [Test]
    public async Task ApplyStop_ForSidecar_UsesSidecarEntryAndItsEnabledIntent()
    {
        var sidecar = Sidecar.Create("traefik", SidecarKind.Traefik);
        sidecar.Enable();
        var entry = ServiceRegistryEntry.CreateForSidecar(sidecar.Id);
        entry.ApplyRuntimeState(ServiceRuntimeState.Running, DateTime.UtcNow.AddMinutes(-10));
        _registry.EnsureSidecarRegisteredAsync(sidecar.Id, Arg.Any<CancellationToken>()).Returns(entry);

        await _sut.ApplyStopAsync(sidecar, ServiceRuntimeState.Crashed, 1, CancellationToken.None);

        entry.RuntimeState.ShouldBe(ServiceRuntimeState.Crashed);
        await _deployments.DidNotReceiveWithAnyArgs().GetActiveStatusesAsync(default!, default);
    }

    [Test]
    public async Task ApplyRunning_FromStopped_RecordsRunning()
    {
        _entry.ApplyRuntimeState(ServiceRuntimeState.Stopped, DateTime.UtcNow);

        var changed = await _sut.ApplyRunningAsync(_service, CancellationToken.None);

        changed.ShouldBeTrue();
        _entry.RuntimeState.ShouldBe(ServiceRuntimeState.Running);
    }

    [Test]
    public async Task ApplyHealth_Unhealthy_WhileRunning_Records()
    {
        await _sut.ApplyHealthAsync(_service, ServiceHealth.Unhealthy, CancellationToken.None);

        _entry.Health.ShouldBe(ServiceHealth.Unhealthy);
    }

    [Test]
    public async Task ApplyHealth_WhileNotRunning_IsIgnored()
    {
        _entry.ApplyRuntimeState(ServiceRuntimeState.Stopped, DateTime.UtcNow);

        var changed = await _sut.ApplyHealthAsync(_service, ServiceHealth.Healthy, CancellationToken.None);

        changed.ShouldBeFalse();
        _entry.Health.ShouldBe(ServiceHealth.Unknown);
    }
}