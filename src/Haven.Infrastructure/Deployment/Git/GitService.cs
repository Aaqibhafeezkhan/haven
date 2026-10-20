using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Models;
using Haven.Domain;
using Haven.Domain.Entities;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;

using Microsoft.Extensions.Logging;

namespace Haven.Infrastructure.Deployment.Git;

public class GitService(
    IGitRepositoryPathProvider repositoryPathProvider,
    IGitCredentialsRepository credentialsRepository,
    IGitProviderFactory gitProviderFactory,
    IRepositoryLockProvider repositoryLocks,
    ILogger<GitService> logger) : IGitService
{
    public async Task<Result<string>> SyncRepositoryAsync(RepositoryBinding binding, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(binding.Repository))
            return Error.Validation("The repository binding requires a repository.");

        try
        {
            var providerResult = await CreateProviderAsync(binding, cancellationToken);
            if (providerResult.IsFailure)
                return providerResult.Error;

            var repositoryPath = repositoryPathProvider.GetRepositoryPath(binding);

            using var _ = await repositoryLocks.AcquireAsync(repositoryPath, cancellationToken);
            await CloneOrPullAsync(providerResult.Value, binding, repositoryPath, cancellationToken);

            return repositoryPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync repository '{Repository}' branch '{Branch}'", binding.Repository, binding.Branch);
            return Error.Failed;
        }
    }

    public async Task<Result<string>> ReadBoundFileAsync(RepositoryBinding binding, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(binding.Repository) || string.IsNullOrWhiteSpace(binding.FilePath))
            return Error.Validation("The repository binding requires a repository and a file path.");

        try
        {
            var providerResult = await CreateProviderAsync(binding, cancellationToken);
            if (providerResult.IsFailure)
                return providerResult.Error;

            var repositoryPath = repositoryPathProvider.GetRepositoryPath(binding);

            // The clone is shared between services, so fetching and reading must not interleave with another caller's.
            using var _ = await repositoryLocks.AcquireAsync(repositoryPath, cancellationToken);
            await CloneOrPullAsync(providerResult.Value, binding, repositoryPath, cancellationToken);

            var fullPath = Path.GetFullPath(Path.Combine(repositoryPath, binding.FilePath));
            var root = Path.GetFullPath(repositoryPath) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.Ordinal))
                return Error.Validation("The bound file path must stay inside the repository.");

            if (!File.Exists(fullPath))
                return Error.NotFoundFor("Repository file", binding.FilePath);

            return await File.ReadAllTextAsync(fullPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read bound file '{FilePath}' from '{Repository}'", binding.FilePath, binding.Repository);
            return Error.Failed;
        }
    }

    private async Task<Result<IGitProvider>> CreateProviderAsync(RepositoryBinding binding, CancellationToken cancellationToken)
    {
        var credentials = binding.GitCredentialId is { } credentialId
            ? await credentialsRepository.GetByIdAsync(credentialId, cancellationToken)
            : null;
        if (binding.GitCredentialId is not null && credentials is null)
            return Error.NotFoundFor("GitCredentials", binding.GitCredentialId.Value);

        return Result<IGitProvider>.Success(gitProviderFactory.Create(credentials?.ProviderType ?? GitProviderType.Generic, credentials));
    }

    private static async Task CloneOrPullAsync(IGitProvider provider, RepositoryBinding binding, string repositoryPath, CancellationToken cancellationToken)
    {
        var branch = string.IsNullOrWhiteSpace(binding.Branch) ? "main" : binding.Branch;

        // LibGit2Sharp.Repository.Clone expects the destination directory to not exist; an empty one is
        // left behind by a failed earlier attempt.
        if (Directory.Exists(repositoryPath) && !Directory.EnumerateFileSystemEntries(repositoryPath).Any())
            Directory.Delete(repositoryPath);

        if (!Directory.Exists(repositoryPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(repositoryPath)!);
            await provider.CloneRepositoryAsync(binding.Repository!, repositoryPath, cancellationToken);
        }

        await provider.PullAsync(repositoryPath, branch, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<string>>> GetRemoteBranchesAsync(string repositoryUrl, GitCredentials? credentials = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = gitProviderFactory.Create(credentials?.ProviderType ?? GitProviderType.Generic, credentials);
            var branches = await provider.GetBranchesAsync(repositoryUrl, cancellationToken);

            return Result<IReadOnlyList<string>>.Success(branches);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get remote branches for repository '{RepositoryUrl}'", repositoryUrl);
            return Error.Failed;
        }
    }

    public async Task<Result<IReadOnlyList<GitRepositorySummary>>> GetAccessibleRepositoriesAsync(GitCredentials credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = gitProviderFactory.Create(credentials.ProviderType, credentials);
            var repositories = await provider.GetAccessibleRepositoriesAsync(cancellationToken);

            return Result<IReadOnlyList<GitRepositorySummary>>.Success(repositories);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get accessible repositories for credential '{CredentialId}'", credentials.Id);
            return Error.Failed;
        }
    }

    public string? GetRepositoryPath(RepositoryBinding binding) =>
        repositoryPathProvider.RepositoryDirectoryExists(binding) ? repositoryPathProvider.GetRepositoryPath(binding) : null;

    public bool RepositoryExists(RepositoryBinding binding) =>
        repositoryPathProvider.RepositoryDirectoryExists(binding);
}