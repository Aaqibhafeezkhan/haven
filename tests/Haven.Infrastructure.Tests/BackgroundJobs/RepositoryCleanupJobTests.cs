using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Configuration;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;
using Haven.Infrastructure.BackgroundJobs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Haven.Infrastructure.Tests.BackgroundJobs;

[Category("Unit")]
public sealed class RepositoryCleanupJobTests
{
    private IGitRepositoryPathProvider _pathProvider = null!;
    private IServiceRepository _serviceRepository = null!;
    private IOptionsMonitor<RepositoryCleanupOptions> _options = null!;
    private RepositoryCleanupOptions _optionsValue = null!;
    private RepositoryCleanupJob _sut = null!;
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "haven-tests", Guid.NewGuid().ToString("N"));

        _pathProvider = Substitute.For<IGitRepositoryPathProvider>();
        _pathProvider.GetRepositoryRootPath().Returns(_root);

        _serviceRepository = Substitute.For<IServiceRepository>();
        _serviceRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(_ => ToAsync());

        _options = Substitute.For<IOptionsMonitor<RepositoryCleanupOptions>>();
        _optionsValue = new RepositoryCleanupOptions { GracePeriodHours = 24, DryRun = false };
        _options.CurrentValue.Returns(_ => _optionsValue);

        _sut = new RepositoryCleanupJob(_pathProvider, _serviceRepository, _options, Substitute.For<ILogger<RepositoryCleanupJob>>());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public async Task ExecuteAsync_WhenNoRepositoryDirectoriesExist_ShouldNotLoadServices()
    {
        await Should.NotThrowAsync(() => _sut.ExecuteAsync());

        _serviceRepository.DidNotReceiveWithAnyArgs().GetAsync(default);
    }

    [Test]
    public async Task ExecuteAsync_ShouldDeleteUnusedRepositoryPastGracePeriod()
    {
        var unused = CreateDirectory("repositories", "repo-unused", DateTime.UtcNow.AddDays(-5));

        await _sut.ExecuteAsync();

        Directory.Exists(unused).ShouldBeFalse();
    }

    [Test]
    public async Task ExecuteAsync_ShouldKeepRepositoryUsedByAnyServiceBinding()
    {
        var binding = new RepositoryBinding { Repository = "https://github.com/org/repo", Branch = "main", FilePath = "a.yaml" };
        var used = CreateDirectory("repositories", "repo-used", DateTime.UtcNow.AddDays(-5));
        var unused = CreateDirectory("repositories", "repo-unused", DateTime.UtcNow.AddDays(-5));
        _pathProvider.GetRepositoryPath(binding).Returns(used);
        _serviceRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(_ => ToAsync(CreateService(binding: binding), CreateService(binding: binding)));

        await _sut.ExecuteAsync();

        Directory.Exists(used).ShouldBeTrue();
        Directory.Exists(unused).ShouldBeFalse();
    }

    [Test]
    public async Task ExecuteAsync_ShouldKeepRepositoryUsedByDockerfileGitSource()
    {
        var used = CreateDirectory("repositories", "repo-used", DateTime.UtcNow.AddDays(-5));
        _pathProvider.GetRepositoryPath(Arg.Any<RepositoryBinding>()).Returns(used);
        var service = CreateService(sourceConfig: new DockerfileConfig
        {
            Source = DockerfileSource.Git,
            Repository = "https://github.com/org/repo",
            Branch = "main",
            FilePath = "Dockerfile"
        });
        _serviceRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(_ => ToAsync(service));

        await _sut.ExecuteAsync();

        Directory.Exists(used).ShouldBeTrue();
    }

    [Test]
    public async Task ExecuteAsync_ShouldKeepUnusedRepositoryWithinGracePeriod()
    {
        var recent = CreateDirectory("repositories", "repo-recent", DateTime.UtcNow);

        await _sut.ExecuteAsync();

        Directory.Exists(recent).ShouldBeTrue();
    }

    [Test]
    public async Task ExecuteAsync_InDryRun_ShouldNotDeleteAnything()
    {
        _optionsValue.DryRun = true;
        var unused = CreateDirectory("repositories", "repo-unused", DateTime.UtcNow.AddDays(-5));

        await _sut.ExecuteAsync();

        Directory.Exists(unused).ShouldBeTrue();
    }

    [Test]
    public async Task ExecuteAsync_ShouldDeleteLegacyPerServiceClonesPastGracePeriod()
    {
        var legacy = CreateDirectory("services", Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(-5));
        var recentLegacy = CreateDirectory("services", Guid.NewGuid().ToString(), DateTime.UtcNow);

        await _sut.ExecuteAsync();

        Directory.Exists(legacy).ShouldBeFalse();
        Directory.Exists(recentLegacy).ShouldBeTrue();
    }

    private string CreateDirectory(string parent, string name, DateTime lastWriteUtc)
    {
        var dir = Path.Combine(_root, parent, name);
        Directory.CreateDirectory(dir);
        Directory.SetLastWriteTimeUtc(dir, lastWriteUtc);
        return dir;
    }

    private static Service CreateService(RepositoryBinding? binding = null, ServiceSourceConfig? sourceConfig = null)
    {
        var service = Service.Create(Guid.NewGuid(), "svc", ServiceType.DockerImage, ExposureMode.Internal,
            sourceConfig: sourceConfig ?? new DockerConfig { Image = "img" });
        service.RepositoryBinding = binding;
        return service;
    }

    private static async IAsyncEnumerable<Service> ToAsync(params Service[] services)
    {
        foreach (var service in services)
            yield return service;
        await Task.CompletedTask;
    }
}
