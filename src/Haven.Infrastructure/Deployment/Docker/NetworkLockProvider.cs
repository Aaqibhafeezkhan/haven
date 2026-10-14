using System.Collections.Concurrent;

namespace Haven.Infrastructure.Deployment.Docker;

/// <summary>
/// Serializes mutations of a single Haven network (provisioning, adoption, recreation) so a deploy and
/// a repair pass can't interleave on it. In-process only: Haven runs as a single instance today. If that
/// ever changes, this is the seam to swap for a DB-backed lock.
/// </summary>
public sealed class NetworkLockProvider
{
    // One small semaphore per network id for the lifetime of the process; the set of networks is tiny.
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(Guid networkId, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(networkId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
        }
    }
}