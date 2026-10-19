using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Domain;
using Haven.Domain.Entities;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;
using Haven.Infrastructure.Deployment.Git;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Haven.Infrastructure.Tests.Deployment.Git;

[Category("Unit")]
public sealed class GitServiceTests
{
    private GitService _sut = null!;
    private IGitRepositoryPathProvider _repositoryPathProvider;
    private IGitCredentialsRepository _credentialsRepository;
    private IGitProviderFactory _gitProviderFactory;
    private IGitProvider _gitProvider;
    private ILogger<GitService> _logger;

    private string _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        _repositoryPathProvider = Substitute.For<IGitRepositoryPathProvider>();
        _credentialsRepository = Substitute.For<IGitCredentialsRepository>();
        _gitProviderFactory = Substitute.For<IGitProviderFactory>();
        _gitProvider = Substitute.For<IGitProvider>();
        _logger = Substitute.For<ILogger<GitService>>();

        _tempRoot = Path.Combine(Path.GetTempPath(), $"haven-git-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);

        _gitProviderFactory.Create(Arg.Any<GitProviderType>(), Arg.Any<GitCredentials?>())
            .Returns(_gitProvider);

        _sut = new GitService(_repositoryPathProvider, _credentialsRepository, _gitProviderFactory, new RepositoryLockProvider(), _logger);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public async Task SyncRepository_WhenNotCloned_ClonesThenPullsBranchAndReturnsPath()
    {
        var binding = CreateBinding(branch: "develop");
        var path = GivenClonePath(binding);

        var result = await _sut.SyncRepositoryAsync(binding);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(path);
        Received.InOrder(() =>
        {
            _gitProvider.CloneRepositoryAsync(binding.Repository!, path, Arg.Any<CancellationToken>());
            _gitProvider.PullAsync(path, "develop", Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task SyncRepository_WhenAlreadyCloned_OnlyPulls()
    {
        var binding = CreateBinding();
        var path = GivenClonePath(binding);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "marker"), "x");

        var result = await _sut.SyncRepositoryAsync(binding);

        result.IsSuccess.ShouldBeTrue();
        await _gitProvider.DidNotReceive().CloneRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _gitProvider.Received(1).PullAsync(path, "main", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SyncRepository_WhenCredentialIsMissing_ReturnsNotFound()
    {
        var binding = CreateBinding(credentialId: Guid.NewGuid());
        GivenClonePath(binding);
        _credentialsRepository.GetByIdAsync(binding.GitCredentialId!.Value, Arg.Any<CancellationToken>()).Returns((GitCredentials?)null);

        var result = await _sut.SyncRepositoryAsync(binding);

        result.IsFailure.ShouldBeTrue();
        await _gitProvider.DidNotReceive().CloneRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SyncRepository_WhenRepositoryIsBlank_ReturnsValidationError()
    {
        var result = await _sut.SyncRepositoryAsync(new RepositoryBinding { Repository = " " });

        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task SyncRepository_WhenCloneThrows_ReturnsFailureAndLogsError()
    {
        var binding = CreateBinding();
        GivenClonePath(binding);
        _gitProvider.CloneRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Clone failed"));

        var result = await _sut.SyncRepositoryAsync(binding);

        result.IsFailure.ShouldBeTrue();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task SyncRepository_WhenPullThrows_ReturnsFailure()
    {
        var binding = CreateBinding();
        var path = GivenClonePath(binding);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "marker"), "x");
        _gitProvider.PullAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Pull failed"));

        var result = await _sut.SyncRepositoryAsync(binding);

        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task SyncRepository_WhenLeftoverEmptyDirectoryExists_ClonesAgain()
    {
        var binding = CreateBinding();
        var path = GivenClonePath(binding);
        Directory.CreateDirectory(path);

        await _sut.SyncRepositoryAsync(binding);

        await _gitProvider.Received(1).CloneRepositoryAsync(binding.Repository!, path, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ReadBoundFile_ReturnsFileContentFromTheSharedClone()
    {
        var binding = CreateBinding();
        var path = GivenClonePath(binding);
        _gitProvider.CloneRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "haven.yaml"), "type: DockerImage");
                return Task.CompletedTask;
            });

        var result = await _sut.ReadBoundFileAsync(binding);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("type: DockerImage");
    }

    [Test]
    public async Task ReadBoundFile_WhenPathEscapesRepository_ReturnsValidationError()
    {
        var binding = CreateBinding();
        binding.FilePath = "../../secret";
        var path = GivenClonePath(binding);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "marker"), "x");

        var result = await _sut.ReadBoundFileAsync(binding);

        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task ReadBoundFile_WhenFileMissing_ReturnsNotFound()
    {
        var binding = CreateBinding();
        var path = GivenClonePath(binding);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "marker"), "x");

        var result = await _sut.ReadBoundFileAsync(binding);

        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task SyncRepository_ForTwoServicesSharingABinding_RunsGitOneAtATime()
    {
        var binding = CreateBinding();
        var path = GivenClonePath(binding);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "marker"), "x");

        var running = 0;
        var maxRunning = 0;
        _gitProvider.PullAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var now = Interlocked.Increment(ref running);
                maxRunning = Math.Max(maxRunning, now);
                await Task.Delay(50);
                Interlocked.Decrement(ref running);
            });

        await Task.WhenAll(_sut.SyncRepositoryAsync(binding), _sut.SyncRepositoryAsync(binding));

        maxRunning.ShouldBe(1);
    }

    [Test]
    public async Task GetRemoteBranches_WhenSuccessful_ReturnsBranches()
    {
        var repositoryUrl = "https://github.com/example/repo.git";
        var branches = new List<string> { "main", "develop", "feature-1" };

        _gitProvider.GetBranchesAsync(repositoryUrl, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(branches));

        var result = await _sut.GetRemoteBranchesAsync(repositoryUrl);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(branches);
    }

    [Test]
    public async Task GetRemoteBranches_WhenProviderThrows_ReturnsFailure()
    {
        var repositoryUrl = "https://github.com/example/repo.git";

        _gitProvider.GetBranchesAsync(repositoryUrl, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Get branches failed"));

        var result = await _sut.GetRemoteBranchesAsync(repositoryUrl);

        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public void GetRepositoryPath_WhenRepositoryExists_ReturnsPath()
    {
        var binding = CreateBinding();
        var path = GivenClonePath(binding);
        _repositoryPathProvider.RepositoryDirectoryExists(binding).Returns(true);

        _sut.GetRepositoryPath(binding).ShouldBe(path);
    }

    [Test]
    public void GetRepositoryPath_WhenRepositoryDoesNotExist_ReturnsNull()
    {
        var binding = CreateBinding();
        _repositoryPathProvider.RepositoryDirectoryExists(binding).Returns(false);

        _sut.GetRepositoryPath(binding).ShouldBeNull();
    }

    [Test]
    public void RepositoryExists_ReflectsPathProvider()
    {
        var binding = CreateBinding();
        _repositoryPathProvider.RepositoryDirectoryExists(binding).Returns(true);

        _sut.RepositoryExists(binding).ShouldBeTrue();
    }

    private string GivenClonePath(RepositoryBinding binding)
    {
        var path = Path.Combine(_tempRoot, "repositories", Guid.NewGuid().ToString("N"));
        _repositoryPathProvider.GetRepositoryPath(binding).Returns(path);
        return path;
    }

    private static RepositoryBinding CreateBinding(string branch = "main", Guid? credentialId = null) => new()
    {
        Repository = "https://github.com/example/repo.git",
        Branch = branch,
        FilePath = "haven.yaml",
        GitCredentialId = credentialId
    };
}
