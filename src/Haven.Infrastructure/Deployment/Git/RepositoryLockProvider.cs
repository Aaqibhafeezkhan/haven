using System.Collections.Concurrent;

using Haven.Application.Common.Interfaces.Deployment;

namespace Haven.Infrastructure.Deployment.Git;

public sealed class RepositoryLockProvider : IRepositoryLockProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task<IDisposable> AcquireAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(Path.GetFullPath(repositoryPath), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                gate.Release();
        }
    }
}