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
    private IGitService _gitService = null!;
    private IServiceRepository _serviceRepository = null!;
    private IOptionsMonitor<RepositoryCleanupOptions> _options = null!;
    private RepositoryCleanupOptions _optionsValue = null!;
    private RepositoryCleanupJob _sut = null!;
    private string _root = null!;
    private string _servicesRoot = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "haven-tests", Guid.NewGuid().ToString("N"));
        _servicesRoot = Path.Combine(_root, "services");

        _pathProvider = Substitute.For<IGitRepositoryPathProvider>();
        _pathProvider.GetRepositoryRootPath().Returns(_root);

        _gitService = Substitute.For<IGitService>();
        _serviceRepository = Substitute.For<IServiceRepository>();
        _options = Substitute.For<IOptionsMonitor<RepositoryCleanupOptions>>();

        _optionsValue = new RepositoryCleanupOptions
        {
            GracePeriodHours = 24,
            DryRun = false
        };
        _options.CurrentValue.Returns(_ => _optionsValue);

        _serviceRepository.FilterMissingIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

        _sut = new RepositoryCleanupJob(
            _pathProvider,
            _gitService,
            _serviceRepository,
            _options,
            Substitute.For<ILogger<RepositoryCleanupJob>>());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string CreateServiceDirectory(Guid serviceId, DateTime? lastWriteUtc = null)
    {
        var dir = Path.Combine(_servicesRoot, serviceId.ToString());
        Directory.CreateDirectory(dir);
        if (lastWriteUtc.HasValue)
            Directory.SetLastWriteTimeUtc(dir, lastWriteUtc.Value);
        return dir;
    }

    [Test]
    public async Task ExecuteAsync_WhenServicesDirectoryDoesNotExist_ShouldNotThrowAndSkip()
    {
        await Should.NotThrowAsync(() => _sut.ExecuteAsync());

        await _serviceRepository.DidNotReceiveWithAnyArgs().FilterMissingIdsAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_WhenNoDirectoriesUnderServices_ShouldNotCallFilterMissingIds()
    {
        Directory.CreateDirectory(_servicesRoot);

        await _sut.ExecuteAsync();

        await _serviceRepository.DidNotReceiveWithAnyArgs().FilterMissingIdsAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_WhenDirectoryNameIsNotAGuid_ShouldIgnoreItAndNotIncludeInFilterCall()
    {
        Directory.CreateDirectory(Path.Combine(_servicesRoot, "not-a-guid"));

        await _sut.ExecuteAsync();

        await _serviceRepository.DidNotReceiveWithAnyArgs().FilterMissingIdsAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_WhenAllDirectoriesExistInDb_ShouldNotCallDeleteServiceRepository()
    {
        CreateServiceDirectory(Guid.NewGuid(), DateTime.UtcNow.AddHours(-48));

        await _sut.ExecuteAsync();

        await _gitService.DidNotReceiveWithAnyArgs().DeleteServiceRepositoryAsync(default, default);
    }

    [Test]
    public async Task ExecuteAsync_WhenDirectoryIsMissingFromDbAndPastGracePeriod_ShouldCallDeleteServiceRepository()
    {
        var danglingId = Guid.NewGuid();
        CreateServiceDirectory(danglingId, DateTime.UtcNow.AddHours(-48));

        _serviceRepository.FilterMissingIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { danglingId });

        await _sut.ExecuteAsync();

        await _gitService.Received(1).DeleteServiceRepositoryAsync(danglingId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenDirectoryIsMissingFromDbButWithinGracePeriod_ShouldNotCallDeleteServiceRepository()
    {
        var danglingId = Guid.NewGuid();
        CreateServiceDirectory(danglingId, DateTime.UtcNow);

        _serviceRepository.FilterMissingIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { danglingId });

        await _sut.ExecuteAsync();

        await _gitService.DidNotReceiveWithAnyArgs().DeleteServiceRepositoryAsync(default, default);
    }

    [Test]
    public async Task ExecuteAsync_WhenDryRunEnabled_ShouldNotCallDeleteServiceRepository()
    {
        var danglingId = Guid.NewGuid();
        CreateServiceDirectory(danglingId, DateTime.UtcNow.AddHours(-48));

        _optionsValue.DryRun = true;
        _serviceRepository.FilterMissingIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { danglingId });

        await _sut.ExecuteAsync();

        await _gitService.DidNotReceiveWithAnyArgs().DeleteServiceRepositoryAsync(default, default);
    }

    [Test]
    public async Task ExecuteAsync_WhenMultipleDanglingDirectoriesExist_ShouldDeleteEachPastGracePeriod()
    {
        var dangling1 = Guid.NewGuid();
        var dangling2 = Guid.NewGuid();
        CreateServiceDirectory(dangling1, DateTime.UtcNow.AddHours(-48));
        CreateServiceDirectory(dangling2, DateTime.UtcNow.AddHours(-72));

        _serviceRepository.FilterMissingIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { dangling1, dangling2 });

        await _sut.ExecuteAsync();

        await _gitService.Received(1).DeleteServiceRepositoryAsync(dangling1, Arg.Any<CancellationToken>());
        await _gitService.Received(1).DeleteServiceRepositoryAsync(dangling2, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ShouldDeleteUnusedManifestRepositoriesAndKeepSharedOnes()
    {
        var binding = new RepositoryBinding { Repository = "https://github.com/org/repo", Branch = "main", FilePath = "a.yaml" };
        var used = CreateManifestDirectory("repo-used", DateTime.UtcNow.AddDays(-5));
        var unused = CreateManifestDirectory("repo-unused", DateTime.UtcNow.AddDays(-5));
        _pathProvider.GetManifestRepositoryPath(binding).Returns(used);
        _serviceRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsync(CreateBoundService(binding), CreateBoundService(binding)));

        await _sut.ExecuteAsync();

        Directory.Exists(used).ShouldBeTrue();
        Directory.Exists(unused).ShouldBeFalse();
    }

    [Test]
    public async Task ExecuteAsync_ShouldKeepUnusedManifestRepositoryWithinGracePeriod()
    {
        var recent = CreateManifestDirectory("repo-recent", DateTime.UtcNow);
        _serviceRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(ToAsync());

        await _sut.ExecuteAsync();

        Directory.Exists(recent).ShouldBeTrue();
    }

    [Test]
    public async Task ExecuteAsync_InDryRun_ShouldNotDeleteUnusedManifestRepositories()
    {
        _optionsValue.DryRun = true;
        var unused = CreateManifestDirectory("repo-unused", DateTime.UtcNow.AddDays(-5));
        _serviceRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(ToAsync());

        await _sut.ExecuteAsync();

        Directory.Exists(unused).ShouldBeTrue();
    }

    private string CreateManifestDirectory(string name, DateTime lastWriteUtc)
    {
        var dir = Path.Combine(_root, "manifests", name);
        Directory.CreateDirectory(dir);
        Directory.SetLastWriteTimeUtc(dir, lastWriteUtc);
        return dir;
    }

    private static Service CreateBoundService(RepositoryBinding binding)
    {
        var service = Service.Create(Guid.NewGuid(), "svc", ServiceType.DockerImage, ExposureMode.Internal,
            sourceConfig: new DockerConfig { Image = "img" });
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
