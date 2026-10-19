using Haven.Domain.Enums;

using Shouldly;

namespace Haven.Domain.Tests;

[TestFixture]
[Category("Unit")]
public sealed class ServiceDisplayStatusResolverTests
{
    [TestCase(DeploymentStatus.Queued)]
    [TestCase(DeploymentStatus.Building)]
    [TestCase(DeploymentStatus.Starting)]
    public void Resolve_ActiveDeployment_IsDeploying_WhateverTheRuntimeState(DeploymentStatus active)
    {
        foreach (var runtime in Enum.GetValues<ServiceRuntimeState>())
        foreach (var health in Enum.GetValues<ServiceHealth>())
            ServiceDisplayStatusResolver.Resolve(active, runtime, health).ShouldBe(ServiceDisplayStatus.Deploying);
    }

    [TestCase(DeploymentStatus.Succeeded)]
    [TestCase(DeploymentStatus.Failed)]
    [TestCase(DeploymentStatus.Cancelled)]
    public void Resolve_FinishedDeployment_IsIgnored(DeploymentStatus finished)
    {
        ServiceDisplayStatusResolver.Resolve(finished, ServiceRuntimeState.Stopped, ServiceHealth.Unknown)
            .ShouldBe(ServiceDisplayStatus.Stopped);
    }

    [TestCase(ServiceRuntimeState.NotDeployed, ServiceHealth.Unknown, ServiceDisplayStatus.NotDeployed)]
    [TestCase(ServiceRuntimeState.NotDeployed, ServiceHealth.Unhealthy, ServiceDisplayStatus.NotDeployed)]
    [TestCase(ServiceRuntimeState.Running, ServiceHealth.Unknown, ServiceDisplayStatus.Running)]
    [TestCase(ServiceRuntimeState.Running, ServiceHealth.Healthy, ServiceDisplayStatus.Running)]
    [TestCase(ServiceRuntimeState.Running, ServiceHealth.Unhealthy, ServiceDisplayStatus.Degraded)]
    [TestCase(ServiceRuntimeState.Restarting, ServiceHealth.Unknown, ServiceDisplayStatus.Starting)]
    [TestCase(ServiceRuntimeState.Restarting, ServiceHealth.Unhealthy, ServiceDisplayStatus.Starting)]
    [TestCase(ServiceRuntimeState.Stopped, ServiceHealth.Unknown, ServiceDisplayStatus.Stopped)]
    [TestCase(ServiceRuntimeState.Stopped, ServiceHealth.Unhealthy, ServiceDisplayStatus.Stopped)]
    [TestCase(ServiceRuntimeState.Crashed, ServiceHealth.Unknown, ServiceDisplayStatus.Crashed)]
    [TestCase(ServiceRuntimeState.Crashed, ServiceHealth.Unhealthy, ServiceDisplayStatus.Crashed)]
    [TestCase(ServiceRuntimeState.Unknown, ServiceHealth.Unknown, ServiceDisplayStatus.Unknown)]
    [TestCase(ServiceRuntimeState.Unknown, ServiceHealth.Healthy, ServiceDisplayStatus.Unknown)]
    public void Resolve_NoActiveDeployment_MapsFromRuntimeStateAndHealth(
        ServiceRuntimeState runtime, ServiceHealth health, ServiceDisplayStatus expected)
    {
        ServiceDisplayStatusResolver.Resolve(null, runtime, health).ShouldBe(expected);
    }

    [Test]
    public void Resolve_NoEntry_IsNotDeployed()
    {
        ServiceDisplayStatusResolver.Resolve(null, null, null).ShouldBe(ServiceDisplayStatus.NotDeployed);
    }

    [Test]
    public void Resolve_CoversEveryRuntimeStateAndHealthCombination()
    {
        foreach (var runtime in Enum.GetValues<ServiceRuntimeState>())
        foreach (var health in Enum.GetValues<ServiceHealth>())
            Should.NotThrow(() => ServiceDisplayStatusResolver.Resolve(null, runtime, health));
    }
}
