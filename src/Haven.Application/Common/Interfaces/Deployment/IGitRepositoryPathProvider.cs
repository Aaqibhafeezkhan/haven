using Haven.Domain.ValueObjects;

namespace Haven.Application.Common.Interfaces.Deployment;

/// <summary>
/// Manages filesystem paths for local git clones. A clone is identified by the repository, branch and
/// git credential of a <see cref="RepositoryBinding"/>, not by the service using it, so services that
/// point at the same triple share one clone while a different branch or credential never reuses it.
/// Storage structure: {root}/repositories/{repoName}-{hash}/
/// </summary>
public interface IGitRepositoryPathProvider
{
    /// <summary>
    /// Gets the root directory where all git repositories are stored.
    /// Typically something like "./git-repositories" or "/data/git-repositories".
    /// </summary>
    string GetRepositoryRootPath();

    /// <summary>Gets the directory where the clone for <paramref name="binding"/> is stored.</summary>
    string GetRepositoryPath(RepositoryBinding binding);

    /// <summary>Checks if a clone already exists for <paramref name="binding"/>.</summary>
    bool RepositoryDirectoryExists(RepositoryBinding binding);
}