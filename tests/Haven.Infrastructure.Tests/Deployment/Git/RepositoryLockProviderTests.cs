using Haven.Infrastructure.Deployment.Git;

using Shouldly;

namespace Haven.Infrastructure.Tests.Deployment.Git;

[Category("Unit")]
public sealed class RepositoryLockProviderTests
{
    private readonly RepositoryLockProvider _sut = new();

    [Test]
    public async Task AcquireAsync_ForSamePath_ShouldWaitUntilReleased()
    {
        var first = await _sut.AcquireAsync("/data/repo-a");

        var second = _sut.AcquireAsync("/data/repo-a");
        await Task.Delay(50);
        second.IsCompleted.ShouldBeFalse();

        first.Dispose();
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }

    [Test]
    public async Task AcquireAsync_ForDifferentPaths_ShouldNotBlock()
    {
        using var first = await _sut.AcquireAsync("/data/repo-a");

        using var second = await _sut.AcquireAsync("/data/repo-b").WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task AcquireAsync_WhenDisposedTwice_ShouldReleaseOnlyOnce()
    {
        var first = await _sut.AcquireAsync("/data/repo-a");
        first.Dispose();
        first.Dispose();

        using var second = await _sut.AcquireAsync("/data/repo-a");
        var third = _sut.AcquireAsync("/data/repo-a");
        await Task.Delay(50);

        third.IsCompleted.ShouldBeFalse();
    }

    [Test]
    public async Task AcquireAsync_WhenCancelledWhileWaiting_ShouldThrow()
    {
        using var held = await _sut.AcquireAsync("/data/repo-a");
        using var cts = new CancellationTokenSource(50);

        await Should.ThrowAsync<OperationCanceledException>(() => _sut.AcquireAsync("/data/repo-a", cts.Token));
    }
}