using Haven.Application.Common.Interfaces.Services;
using Haven.Application.Features.Services;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;

using Shouldly;

namespace Haven.Application.Tests.Features.Services;

[Category("Unit")]
public sealed class ServiceStatisticsCalculatorTests
{
    private readonly Dictionary<Guid, ServiceRegistryEntry> _entries = [];
    private readonly Dictionary<Guid, DeploymentStatus> _active = [];

    private Service NewService(ServiceRuntimeState? runtime = null, ServiceHealth health = ServiceHealth.Unknown, DeploymentStatus? deployment = null)
    {
        var service = Service.Create(Guid.NewGuid(), "svc" + _entries.Count + _active.Count, ServiceType.DockerImage, ExposureMode.None);
        if (runtime is { } state)
        {
            var entry = ServiceRegistryEntry.Create(service.Id);
            entry.ApplyRuntimeState(state, DateTime.UtcNow);
            if (health != ServiceHealth.Unknown) entry.ApplyHealth(health);
            _entries[service.Id] = entry;
        }

        if (deployment is { } d) _active[service.Id] = d;
        return service;
    }

    private ServiceStateSnapshot Snapshot() => new(_entries, _active);

    [Test]
    public void Compute_BucketsEachDisplayStatus()
    {
        var services = new[]
        {
            NewService(ServiceRuntimeState.Running, ServiceHealth.Healthy),
            NewService(ServiceRuntimeState.Running, ServiceHealth.Unhealthy),
            NewService(ServiceRuntimeState.Stopped),
            NewService(ServiceRuntimeState.Crashed),
            NewService(),
            NewService(ServiceRuntimeState.Stopped, deployment: DeploymentStatus.Queued),
            NewService(ServiceRuntimeState.Stopped, deployment: DeploymentStatus.Building),
            NewService(ServiceRuntimeState.Restarting),
            NewService(ServiceRuntimeState.Unknown),
        };

        var stats = ServiceStatisticsCalculator.Compute(services, Snapshot());

        stats.Total.ShouldBe(9);
        stats.Running.ShouldBe(2);
        stats.Degraded.ShouldBe(1);
        stats.Stopped.ShouldBe(3);
        stats.DeploymentPending.ShouldBe(1);
        stats.Deploying.ShouldBe(2);
        stats.Unknown.ShouldBe(1);
    }

    [Test]
    public void GetEnvironmentStatus_MatchesTheOriginalRules()
    {
        ServiceStatisticsCalculator.GetEnvironmentStatus(new()).ShouldBe(HealthStatus.Unknown);
        ServiceStatisticsCalculator.GetEnvironmentStatus(new() { Total = 2, Running = 2 }).ShouldBe(HealthStatus.Healthy);
        ServiceStatisticsCalculator.GetEnvironmentStatus(new() { Total = 2, Running = 1 }).ShouldBe(HealthStatus.Degraded);
        ServiceStatisticsCalculator.GetEnvironmentStatus(new() { Total = 2, Deploying = 1 }).ShouldBe(HealthStatus.Deploying);
        ServiceStatisticsCalculator.GetEnvironmentStatus(new() { Total = 2, DeploymentPending = 1 }).ShouldBe(HealthStatus.DeploymentPending);
        ServiceStatisticsCalculator.GetEnvironmentStatus(new() { Total = 2, Stopped = 2 }).ShouldBe(HealthStatus.Stopped);
    }

    [Test]
    public void Sum_AddsEveryBucket()
    {
        var sum = ServiceStatisticsCalculator.Sum([
            new() { Total = 2, Running = 1, Stopped = 1 },
            new() { Total = 1, Degraded = 1, Running = 1 }
        ]);

        sum.Total.ShouldBe(3);
        sum.Running.ShouldBe(2);
        sum.Stopped.ShouldBe(1);
        sum.Degraded.ShouldBe(1);
    }
}