namespace Haven.Application.Common.Interfaces.Deployment;

/// <summary>
/// Hands out one lock per local repository path, so callers sharing a clone on disk never run git
/// against it concurrently. Must be registered as a singleton: the locks only work if every caller
/// resolves the same instance.
/// </summary>
public interface IRepositoryLockProvider
{
    /// <summary>
    /// Waits for exclusive access to the repository at <paramref name="repositoryPath"/>.
    /// Dispose the result to release it.
    /// </summary>
    Task<IDisposable> AcquireAsync(string repositoryPath, CancellationToken cancellationToken = default);
}