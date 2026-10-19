using Haven.Domain.Aggregates;
using Haven.Domain.Entities;
using Haven.Domain.Enums;
using Haven.Domain.Events;
using Haven.Domain.Exceptions;

using Shouldly;

namespace Haven.Domain.Tests.Aggregates;

[TestFixture]
[Category("Unit")]
public sealed class ServiceDeploymentTests
{
    private static Service NewService() =>
        Service.Create(Guid.NewGuid(), "test-service", ServiceType.DockerImage, ExposureMode.None);

    [Test]
    public void Create_DefaultsToStoppedDesiredState()
    {
        NewService().DesiredState.ShouldBe(DesiredState.Stopped);
    }

    [Test]
    public void BeginDeployment_QueuesDeploymentAndExposesItAsActive()
    {
        var service = NewService();

        var deployment = service.BeginDeployment("log.txt", "tester");

        deployment.Status.ShouldBe(DeploymentStatus.Queued);
        service.ActiveDeployment.ShouldBe(deployment);
        service.DomainEvents.ShouldContain(e => e is ServiceDeployingEvent);
    }

    [Test]
    public void BeginDeployment_WhileAnotherIsActive_Throws()
    {
        var service = NewService();
        service.BeginDeployment("a.txt");

        Should.Throw<ValidationException>(() => service.BeginDeployment("b.txt"));
    }

    [Test]
    public void CompleteDeployment_SetsDesiredRunning_ClearsActiveAndRaisesDeployed()
    {
        var service = NewService();
        var deployment = service.BeginDeployment("log.txt");
        deployment.StartBuilding();
        deployment.StartStarting();

        service.CompleteDeployment(deployment);

        deployment.Status.ShouldBe(DeploymentStatus.Succeeded);
        service.ActiveDeployment.ShouldBeNull();
        service.DesiredState.ShouldBe(DesiredState.Running);
        service.LastDeployedAt.ShouldNotBeNull();
        service.DomainEvents.ShouldContain(e => e is ServiceDeployedEvent);
    }

    [Test]
    public void FailDeployment_LeavesDesiredStateUntouched()
    {
        var service = NewService();
        var deployment = service.BeginDeployment("log.txt");

        service.FailDeployment(deployment);

        deployment.Status.ShouldBe(DeploymentStatus.Failed);
        service.ActiveDeployment.ShouldBeNull();
        service.DesiredState.ShouldBe(DesiredState.Stopped);
    }

    [Test]
    public void Deployment_TerminalStatus_CannotTransitionAgain()
    {
        var service = NewService();
        var deployment = service.BeginDeployment("log.txt");
        service.CancelDeployment(deployment);

        Should.Throw<ValidationException>(() => deployment.Fail());
        Should.Throw<ValidationException>(() => deployment.StartBuilding());
    }

    [Test]
    public void Deployment_CompleteBeforeStarting_Throws()
    {
        var deployment = Deployment.Create(Guid.NewGuid(), "log.txt");

        Should.Throw<ValidationException>(() => deployment.Complete());
    }

    [Test]
    public void SetDesiredState_Stopped_RaisesStoppedEventOnlyOnChange()
    {
        var service = NewService();
        service.SetDesiredState(DesiredState.Running);
        service.ClearDomainEvents();

        service.SetDesiredState(DesiredState.Stopped);
        service.SetDesiredState(DesiredState.Stopped);

        service.DomainEvents.Count(e => e is ServiceStoppedEvent).ShouldBe(1);
    }

    [TestCase(DeploymentStatus.Queued, false)]
    [TestCase(DeploymentStatus.Building, false)]
    [TestCase(DeploymentStatus.Starting, false)]
    [TestCase(DeploymentStatus.Succeeded, true)]
    [TestCase(DeploymentStatus.Failed, true)]
    [TestCase(DeploymentStatus.Cancelled, true)]
    public void IsTerminal_MatchesStatus(DeploymentStatus status, bool expected)
    {
        status.IsTerminal.ShouldBe(expected);
    }
}