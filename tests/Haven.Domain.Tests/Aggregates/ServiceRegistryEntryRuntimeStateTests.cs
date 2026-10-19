using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.Events;

using Shouldly;

namespace Haven.Domain.Tests.Aggregates;

[TestFixture]
[Category("Unit")]
public sealed class ServiceRegistryEntryRuntimeStateTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ServiceRegistryEntry NewRunningEntry()
    {
        var entry = ServiceRegistryEntry.Create(Guid.NewGuid());
        entry.ApplyRuntimeState(ServiceRuntimeState.Running, Now);
        entry.ClearDomainEvents();
        return entry;
    }

    [Test]
    public void New_StartsNotDeployedWithUnknownHealth()
    {
        var entry = ServiceRegistryEntry.Create(Guid.NewGuid());

        entry.RuntimeState.ShouldBe(ServiceRuntimeState.NotDeployed);
        entry.Health.ShouldBe(ServiceHealth.Unknown);
        entry.StateChangedAt.ShouldBe(entry.RegisteredAt);
    }

    [Test]
    public void OwnerKind_ReflectsWhichOwnerIsSet()
    {
        var serviceId = Guid.NewGuid();
        var sidecarId = Guid.NewGuid();

        var forService = ServiceRegistryEntry.Create(serviceId);
        var forSidecar = ServiceRegistryEntry.CreateForSidecar(sidecarId);

        forService.OwnerKind.ShouldBe(OwnerKind.Service);
        forService.OwnerId.ShouldBe(serviceId);
        forSidecar.OwnerKind.ShouldBe(OwnerKind.Sidecar);
        forSidecar.OwnerId.ShouldBe(sidecarId);
    }

    [Test]
    public void ApplyRuntimeState_Changed_UpdatesStateRaisesEventAndReturnsTrue()
    {
        var entry = ServiceRegistryEntry.Create(Guid.NewGuid());

        var changed = entry.ApplyRuntimeState(ServiceRuntimeState.Running, Now);

        changed.ShouldBeTrue();
        entry.RuntimeState.ShouldBe(ServiceRuntimeState.Running);
        entry.StateChangedAt.ShouldBe(Now);
        entry.StartedAt.ShouldBe(Now);
        var evt = entry.DomainEvents.OfType<ServiceRuntimeStateChangedEvent>().ShouldHaveSingleItem();
        evt.Previous.ShouldBe(ServiceRuntimeState.NotDeployed);
        evt.Next.ShouldBe(ServiceRuntimeState.Running);
        evt.OwnerKind.ShouldBe(OwnerKind.Service);
        evt.OwnerId.ShouldBe(entry.ServiceId!.Value);
    }

    [Test]
    public void ApplyRuntimeState_Same_ReturnsFalseAndRaisesNothing()
    {
        var entry = NewRunningEntry();

        var changed = entry.ApplyRuntimeState(ServiceRuntimeState.Running, Now.AddMinutes(5));

        changed.ShouldBeFalse();
        entry.StateChangedAt.ShouldBe(Now);
        entry.DomainEvents.ShouldBeEmpty();
    }

    [Test]
    public void ApplyRuntimeState_LeavingRunning_ClearsStartedAtAndResetsHealth()
    {
        var entry = NewRunningEntry();
        entry.ApplyHealth(ServiceHealth.Healthy);
        entry.ClearDomainEvents();

        entry.ApplyRuntimeState(ServiceRuntimeState.Crashed, Now.AddMinutes(1));

        entry.StartedAt.ShouldBeNull();
        entry.Health.ShouldBe(ServiceHealth.Unknown);
        entry.DomainEvents.OfType<ServiceRuntimeStateChangedEvent>().ShouldHaveSingleItem();
        var health = entry.DomainEvents.OfType<ServiceHealthChangedEvent>().ShouldHaveSingleItem();
        health.Previous.ShouldBe(ServiceHealth.Healthy);
        health.Next.ShouldBe(ServiceHealth.Unknown);
    }

    [Test]
    public void ApplyRuntimeState_LeavingRunningWithUnknownHealth_RaisesNoHealthEvent()
    {
        var entry = NewRunningEntry();

        entry.ApplyRuntimeState(ServiceRuntimeState.Stopped, Now.AddMinutes(1));

        entry.DomainEvents.OfType<ServiceHealthChangedEvent>().ShouldBeEmpty();
    }

    [Test]
    public void ApplyHealth_WhileRunning_ChangesAndRaisesEvent()
    {
        var entry = NewRunningEntry();

        var changed = entry.ApplyHealth(ServiceHealth.Unhealthy);

        changed.ShouldBeTrue();
        entry.Health.ShouldBe(ServiceHealth.Unhealthy);
        var evt = entry.DomainEvents.OfType<ServiceHealthChangedEvent>().ShouldHaveSingleItem();
        evt.Previous.ShouldBe(ServiceHealth.Unknown);
        evt.Next.ShouldBe(ServiceHealth.Unhealthy);
    }

    [Test]
    public void ApplyHealth_SameValue_ReturnsFalse()
    {
        var entry = NewRunningEntry();
        entry.ApplyHealth(ServiceHealth.Healthy);
        entry.ClearDomainEvents();

        entry.ApplyHealth(ServiceHealth.Healthy).ShouldBeFalse();
        entry.DomainEvents.ShouldBeEmpty();
    }

    [TestCase(ServiceRuntimeState.NotDeployed)]
    [TestCase(ServiceRuntimeState.Restarting)]
    [TestCase(ServiceRuntimeState.Stopped)]
    [TestCase(ServiceRuntimeState.Crashed)]
    [TestCase(ServiceRuntimeState.Unknown)]
    public void ApplyHealth_WhenNotRunning_IsIgnored(ServiceRuntimeState state)
    {
        var entry = ServiceRegistryEntry.CreateForSidecar(Guid.NewGuid());
        entry.ApplyRuntimeState(state, Now);
        entry.ClearDomainEvents();

        var changed = entry.ApplyHealth(ServiceHealth.Healthy);

        changed.ShouldBeFalse();
        entry.Health.ShouldBe(ServiceHealth.Unknown);
        entry.DomainEvents.ShouldBeEmpty();
    }
}