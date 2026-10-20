using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Configuration;
using Haven.Domain.Aggregates;
using Haven.Domain.ValueObjects;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Haven.Infrastructure.BackgroundJobs;

/// <summary>
/// Removes local git clones nothing points at any more. Clones are shared, so one is dangling only when
/// no service's repository binding and no service's or sidecar's Dockerfile source resolves to it.
/// </summary>
public sealed class RepositoryCleanupJob(
    IGitRepositoryPathProvider pathProvider,
    IServiceRepository serviceRepository,
    ISidecarRepository sidecarRepository,
    IRepositoryLockProvider repositoryLocks,
    IOptionsMonitor<RepositoryCleanupOptions> options,
    ILogger<RepositoryCleanupJob> logger)
{
    public async Task ExecuteAsync()
    {
        var current = options.CurrentValue;
        logger.LogInformation(
            "Running repository cleanup (grace period: {GracePeriodHours}h, dry run: {DryRun})",
            current.GracePeriodHours, current.DryRun);

        var root = pathProvider.GetRepositoryRootPath();

        // "services" holds the per-service clones from before clones were keyed by repository; nothing uses them now.
        var candidates = new List<string>();
        foreach (var directory in new[] { "repositories", "services" })
        {
            var path = Path.Combine(root, directory);
            if (Directory.Exists(path))
                candidates.AddRange(Directory.GetDirectories(path));
        }

        if (candidates.Count == 0)
        {
            logger.LogInformation("No repository directories to evaluate");
            return;
        }

        var inUse = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var service in serviceRepository.GetAsync(CancellationToken.None))
        {
            foreach (var binding in BindingsOf(service))
                inUse.Add(Path.GetFullPath(pathProvider.GetRepositoryPath(binding)));
        }

        foreach (var sidecar in await sidecarRepository.GetAllAsync(CancellationToken.None))
        {
            if (DockerfileBindingOf(sidecar.SourceConfig) is { } binding)
                inUse.Add(Path.GetFullPath(pathProvider.GetRepositoryPath(binding)));
        }

        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(current.GracePeriodHours);
        var deletedCount = 0;

        foreach (var dir in candidates)
        {
            if (inUse.Contains(Path.GetFullPath(dir)))
                continue;

            var lastWriteUtc = Directory.GetLastWriteTimeUtc(dir);
            if (lastWriteUtc > cutoff)
            {
                logger.LogInformation(
                    "Skipping unused repository '{Path}': within grace period (last write {LastWrite})",
                    dir, lastWriteUtc);
                continue;
            }

            if (current.DryRun)
            {
                logger.LogInformation("[DryRun] Would delete unused repository directory '{Path}'", dir);
                continue;
            }

            try
            {
                // Same lock git operations take, so a clone is never deleted while a pull or build-context read is using it.
                using var _ = await repositoryLocks.AcquireAsync(dir);
                Directory.Delete(dir, recursive: true);
                deletedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete unused repository directory '{Path}'", dir);
            }
        }

        logger.LogInformation("Repository cleanup completed: {Count} unused repository(ies) removed", deletedCount);
    }

    private static IEnumerable<RepositoryBinding> BindingsOf(Service service)
    {
        if (service.RepositoryBinding is { } binding)
            yield return binding;
        if (DockerfileBindingOf(service.SourceConfig) is { } source)
            yield return source;
    }

    private static RepositoryBinding? DockerfileBindingOf(ServiceSourceConfig? sourceConfig) =>
        (sourceConfig as DockerfileConfig)?.ToRepositoryBinding();
}