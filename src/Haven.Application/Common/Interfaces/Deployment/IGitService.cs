using Haven.Application.Common;
using Haven.Application.Common.Models;
using Haven.Domain.Entities;
using Haven.Domain.ValueObjects;

namespace Haven.Application.Common.Interfaces.Deployment;

public interface IGitService
{
    /// <summary>
    /// Clones the repository named by <paramref name="binding"/> if it isn't on disk yet, then pulls
    /// the bound branch, using the binding's own git credential. Returns the local clone path.
    /// Services with the same repository, branch and credential share one clone; access to it is serialized.
    /// Storage: {root}/repositories/{repoName}-{hash}
    /// </summary>
    Task<Result<string>> SyncRepositoryAsync(
        RepositoryBinding binding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs the repository like <see cref="SyncRepositoryAsync"/> and returns the content of the
    /// bound file at the tip of the bound branch.
    /// </summary>
    Task<Result<string>> ReadBoundFileAsync(
        RepositoryBinding binding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available branches from a remote repository.
    /// Optionally authenticates using the provided git credentials.
    /// Does not require a local clone to exist.
    /// </summary>
    Task<Result<IReadOnlyList<string>>> GetRemoteBranchesAsync(
        string repositoryUrl,
        GitCredentials? credentials = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets repositories the given git credential owns or has access to.
    /// </summary>
    Task<Result<IReadOnlyList<GitRepositorySummary>>> GetAccessibleRepositoriesAsync(
        GitCredentials credentials,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the local clone path for a binding if it exists.
    /// </summary>
    string? GetRepositoryPath(RepositoryBinding binding);

    /// <summary>
    /// Checks if the repository of a binding has been cloned locally.
    /// </summary>
    bool RepositoryExists(RepositoryBinding binding);
}