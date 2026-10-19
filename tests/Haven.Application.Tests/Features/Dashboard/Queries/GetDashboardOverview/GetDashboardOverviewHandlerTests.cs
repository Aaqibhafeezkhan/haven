using Haven.Application.Common.Interfaces.Services;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Features.Dashboard.Queries.GetDashboardOverview;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;

using NSubstitute;

using Shouldly;

namespace Haven.Application.Tests.Features.Dashboard.Queries.GetDashboardOverview;

[Category("Unit")]
public sealed class GetDashboardOverviewHandlerTests
{
    private IProjectRepository _projectRepository;
    private IServiceStateReader _stateReader;
    private Dictionary<Guid, ServiceRegistryEntry> _entries;
    private GetDashboardOverviewHandler _sut;

    [SetUp]
    public void Setup()
    {
        _projectRepository = Substitute.For<IProjectRepository>();
        _stateReader = Substitute.For<IServiceStateReader>();
        _entries = [];
        _stateReader.LoadAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new ServiceStateSnapshot(_entries, new Dictionary<Guid, DeploymentStatus>())));
        _sut = new GetDashboardOverviewHandler(_projectRepository, _stateReader);
    }

    [Test]
    public async Task Handle_ShouldReturnZeroedOverview_WhenThereAreNoProjects()
    {
        _projectRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(ToAsyncEnumerable());

        var result = await _sut.Handle(new GetDashboardOverviewQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalProjects.ShouldBe(0);
        result.Value.TotalEnvironments.ShouldBe(0);
        result.Value.ServiceStatistics.Total.ShouldBe(0);
        result.Value.AttentionEnvironment.ShouldBeNull();
        result.Value.DeploymentsLast24h.ShouldBe(0);
        result.Value.LastDeployment.ShouldBeNull();
    }

    [Test]
    public async Task Handle_ShouldPickMostSevereEnvironment_WhenMultipleProjectsHaveIssues()
    {
        var healthyProject = Project.Create("healthy-project");
        var healthyEnv = healthyProject.AddEnvironment("prod");
        var healthyService = healthyEnv.AddService("api", ServiceType.DockerImage, ExposureMode.Internal);
        SetRunning(healthyService.Id);

        var stoppedProject = Project.Create("stopped-project");
        var stoppedEnv = stoppedProject.AddEnvironment("prod");
        stoppedEnv.AddService("worker", ServiceType.DockerImage, ExposureMode.Internal);

        var degradedProject = Project.Create("degraded-project");
        var degradedEnv = degradedProject.AddEnvironment("prod");
        var runningService = degradedEnv.AddService("web", ServiceType.DockerImage, ExposureMode.Internal);
        degradedEnv.AddService("queue", ServiceType.DockerImage, ExposureMode.Internal);
        SetRunning(runningService.Id);

        _projectRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(healthyProject, stoppedProject, degradedProject));

        var result = await _sut.Handle(new GetDashboardOverviewQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalProjects.ShouldBe(3);
        result.Value.TotalEnvironments.ShouldBe(3);
        result.Value.ServiceStatistics.Total.ShouldBe(4);
        result.Value.ServiceStatistics.Running.ShouldBe(2);

        result.Value.AttentionEnvironment.ShouldNotBeNull();
        result.Value.AttentionEnvironment.ProjectName.ShouldBe("degraded-project");
        result.Value.AttentionEnvironment.Status.ShouldBe(HealthStatus.Degraded);
        result.Value.AttentionEnvironment.AffectedServiceCount.ShouldBe(1);
    }

    [Test]
    public async Task Handle_ShouldCountRunningButUnhealthyServices_AsAffected()
    {
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("prod");
        var unhealthyService = environment.AddService("api", ServiceType.DockerImage, ExposureMode.Internal);
        SetRunning(unhealthyService.Id, ServiceHealth.Unhealthy);

        var healthyService = environment.AddService("worker", ServiceType.DockerImage, ExposureMode.Internal);
        SetRunning(healthyService.Id);

        _projectRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(ToAsyncEnumerable(project));

        var result = await _sut.Handle(new GetDashboardOverviewQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AttentionEnvironment.ShouldNotBeNull();
        result.Value.AttentionEnvironment.AffectedServiceCount.ShouldBe(1);
    }

    [Test]
    public async Task Handle_ShouldCountRecentDeploysAndReportTheLatestOne()
    {
        var project = Project.Create("test-project");
        var environment = project.AddEnvironment("prod");
        var recentService = environment.AddService("api", ServiceType.DockerImage, ExposureMode.Internal);
        recentService.LastDeployedAt = DateTime.UtcNow.AddHours(-1);

        var oldService = environment.AddService("worker", ServiceType.DockerImage, ExposureMode.Internal);
        oldService.LastDeployedAt = DateTime.UtcNow.AddDays(-3);

        _projectRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(ToAsyncEnumerable(project));

        var result = await _sut.Handle(new GetDashboardOverviewQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DeploymentsLast24h.ShouldBe(1);
        result.Value.LastDeployment.ShouldNotBeNull();
        result.Value.LastDeployment.ServiceName.ShouldBe("api");
    }

    private void SetRunning(Guid serviceId, ServiceHealth health = ServiceHealth.Healthy)
    {
        var entry = ServiceRegistryEntry.Create(serviceId);
        entry.ApplyRuntimeState(ServiceRuntimeState.Running, DateTime.UtcNow);
        entry.ApplyHealth(health);
        _entries[serviceId] = entry;
    }

    private static async IAsyncEnumerable<Project> ToAsyncEnumerable(params Project[] projects)
    {
        foreach (var project in projects)
        {
            yield return project;
        }

        await Task.CompletedTask;
    }
}