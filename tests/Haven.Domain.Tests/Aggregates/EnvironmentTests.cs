using Haven.Domain.Aggregates;
using Haven.Domain.Enums;

using Shouldly;

using Environment = Haven.Domain.Aggregates.Environment;

namespace Haven.Domain.Tests.Aggregates;

[TestFixture]
[Category("Unit")]
public sealed class EnvironmentTests
{
    private static (Environment Environment, Service Service) NewEnvironmentWithService()
    {
        var environment = Environment.Create(Guid.NewGuid(), "dev");
        var service = environment.AddService("svc", ServiceType.DockerImage, ExposureMode.None);
        return (environment, service);
    }

    [Test]
    public void RemoveService_DesiredRunning_Succeeds()
    {
        var (environment, service) = NewEnvironmentWithService();
        service.SetDesiredState(DesiredState.Running);

        environment.RemoveService(service.Id);

        environment.Services.ShouldNotContain(service);
    }

    [Test]
    public void SetServiceDesiredState_UpdatesService()
    {
        var (environment, service) = NewEnvironmentWithService();

        environment.SetServiceDesiredState(service.Id, DesiredState.Running);

        service.DesiredState.ShouldBe(DesiredState.Running);
    }
}
