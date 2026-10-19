using Haven.Domain.Aggregates;

namespace Haven.Application.Common.Interfaces.Deployment;

/// <summary>
/// Reconciles a repository-owned <see cref="Service"/> with the manifest file its
/// <see cref="Haven.Domain.ValueObjects.RepositoryBinding"/> points at, so the repo is the source of
/// truth for the declarative parts of the service. Mutations are applied to the tracked entity and
/// persisted by the caller's next save.
/// </summary>
public interface IRepositoryManifestSyncService
{
    /// <summary>
    /// Fetches the bound manifest and applies it to <paramref name="service"/>.
    /// Returns whether anything on the service changed.
    /// </summary>
    Task<Result<bool>> SyncAsync(Service service, CancellationToken cancellationToken);
}
