using Haven.Domain.ValueObjects;

namespace Haven.Application.Common.Interfaces.Deployment;

/// <summary>
/// Manages filesystem paths for git repositories, stored separately from manifests.
/// Each service has a single repository shared across all environments (different branches per environment).
/// Storage structure: {root}/services/{serviceId}/
/// </summary>
public interface IGitRepositoryPathProvider
{
    /// <summary>
    /// Gets the root directory where all git repositories are stored.
    /// Typically something like "./git-repositories" or "/data/git-repositories".
    /// </summary>
    string GetRepositoryRootPath();

    /// <summary>
    /// Gets the directory path where a service's repository should be cloned/stored.
    /// The repository is shared across all environments; different branches are checked out per environment.
    /// Format: {root}/services/{serviceId}
    /// </summary>
    string GetServiceRepositoryPath(Guid serviceId);

    /// <summary>
    /// Gets the directory path where the manifest repository of a <see cref="RepositoryBinding"/> is cloned.
    /// Kept apart from <see cref="GetServiceRepositoryPath"/> because the manifest repo and the
    /// Dockerfile source repo of a service can differ. The path is derived from the repository, branch
    /// and git credential, so services bound to the same triple share one clone while a different
    /// credential or branch never reuses it.
    /// Format: {root}/manifests/{repoName}-{hash}
    /// </summary>
    string GetManifestRepositoryPath(RepositoryBinding binding);

    /// <summary>
    /// Ensures the directory structure exists for a service's repository.
    /// </summary>
    Task EnsureRepositoryDirectoryExistsAsync(Guid serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a repository directory exists for the given service.
    /// </summary>
    bool RepositoryDirectoryExists(Guid serviceId);
}