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
    public async Task<Result<string>> CloneServiceRepositoryAsync(Guid serviceId, string repositoryUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var destinationPath = repositoryPathProvider.GetServiceRepositoryPath(serviceId);

            // Ensure parent directory exists but NOT the destination itself
            // LibGit2Sharp.Repository.Clone expects the destination directory to not exist
            var parentPath = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parentPath) && !Directory.Exists(parentPath))
            {
                Directory.CreateDirectory(parentPath);
            }

            // Remove destination if it exists and is empty (from failed previous attempts)
            if (Directory.Exists(destinationPath) && !Directory.EnumerateFileSystemEntries(destinationPath).Any())
            {
                Directory.Delete(destinationPath);
            }

            var credentials = await credentialsRepository.GetByServiceIdAsync(serviceId, cancellationToken);
            var provider = gitProviderFactory.Create(credentials?.ProviderType ?? GitProviderType.Generic, credentials);
            await provider.CloneRepositoryAsync(repositoryUrl, destinationPath, cancellationToken);

            logger.LogInformation("Repository cloned for service '{ServiceId}' to path '{Path}'", serviceId, destinationPath);
            return destinationPath;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clone repository for service '{ServiceId}'", serviceId);
            return Error.Failed;
        }
    }

    public async Task<Result> PullServiceRepositoryAsync(Guid serviceId, string branch, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = repositoryPathProvider.GetServiceRepositoryPath(serviceId);

            if (!repositoryPathProvider.RepositoryDirectoryExists(serviceId))
            {
                logger.LogWarning("Repository not found for service '{ServiceId}' at path '{Path}'", serviceId, repositoryPath);
                return Error.NotFoundFor("Repository", serviceId);
            }

            var credentials = await credentialsRepository.GetByServiceIdAsync(serviceId, cancellationToken);
            var provider = gitProviderFactory.Create(credentials?.ProviderType ?? GitProviderType.Generic, credentials);
            await provider.PullAsync(repositoryPath, branch, cancellationToken);

            logger.LogInformation("Repository pulled for service '{ServiceId}' branch '{Branch}'", serviceId, branch);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to pull repository for service '{ServiceId}'", serviceId);
            return Error.Failed;
        }
    }

    public async Task<Result<string>> ReadBoundFileAsync(RepositoryBinding binding, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(binding.Repository) || string.IsNullOrWhiteSpace(binding.FilePath))
            return Error.Validation("The repository binding requires a repository and a file path.");

        try
        {
            var repositoryPath = repositoryPathProvider.GetManifestRepositoryPath(binding);
            var branch = string.IsNullOrWhiteSpace(binding.Branch) ? "main" : binding.Branch;

            var credentials = binding.GitCredentialId is { } credentialId
                ? await credentialsRepository.GetByIdAsync(credentialId, cancellationToken)
                : null;
            if (binding.GitCredentialId is not null && credentials is null)
                return Error.NotFoundFor("GitCredentials", binding.GitCredentialId.Value);

            var provider = gitProviderFactory.Create(credentials?.ProviderType ?? GitProviderType.Generic, credentials);

            // The clone is shared between services, so fetching and reading must not interleave with another caller's.
            using var _ = await repositoryLocks.AcquireAsync(repositoryPath, cancellationToken);

            if (!Directory.Exists(repositoryPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(repositoryPath)!);
                await provider.CloneRepositoryAsync(binding.Repository, repositoryPath, cancellationToken);
            }

            await provider.PullAsync(repositoryPath, branch, cancellationToken);

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

    public string? GetServiceRepositoryPath(Guid serviceId)
    {
        if (!repositoryPathProvider.RepositoryDirectoryExists(serviceId))
            return null;

        return repositoryPathProvider.GetServiceRepositoryPath(serviceId);
    }

    public bool ServiceRepositoryExists(Guid serviceId)
    {
        return repositoryPathProvider.RepositoryDirectoryExists(serviceId);
    }

    public async Task DeleteServiceRepositoryAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = repositoryPathProvider.GetServiceRepositoryPath(serviceId);

            if (Directory.Exists(repositoryPath))
            {
                Directory.Delete(repositoryPath, recursive: true);
                logger.LogInformation("Repository deleted for service '{ServiceId}'", serviceId);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete repository for service '{ServiceId}'", serviceId);
        }
    }
}