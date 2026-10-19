using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Haven.Infrastructure.BackgroundJobs;

public sealed class RepositoryCleanupJob(
    IGitRepositoryPathProvider pathProvider,
    IGitService gitService,
    IServiceRepository serviceRepository,
    IOptionsMonitor<RepositoryCleanupOptions> options,
    ILogger<RepositoryCleanupJob> logger)
{
    public async Task ExecuteAsync()
    {
        var current = options.CurrentValue;
        logger.LogInformation(
            "Running repository cleanup (grace period: {GracePeriodHours}h, dry run: {DryRun})",
            current.GracePeriodHours, current.DryRun);

        await CleanupServiceRepositoriesAsync(current);
        await CleanupManifestRepositoriesAsync(current);
    }

    private async Task CleanupServiceRepositoriesAsync(RepositoryCleanupOptions current)
    {
        var servicesRoot = Path.Combine(pathProvider.GetRepositoryRootPath(), "services");
        if (!Directory.Exists(servicesRoot))
        {
            logger.LogInformation("No services directory found at '{Path}', nothing to clean up", servicesRoot);
            return;
        }

        var directoriesById = new Dictionary<Guid, string>();
        foreach (var dir in Directory.GetDirectories(servicesRoot))
        {
            if (Guid.TryParse(Path.GetFileName(dir), out var serviceId))
                directoriesById[serviceId] = dir;
        }

        if (directoriesById.Count == 0)
        {
            logger.LogInformation("No repository directories to evaluate");
            return;
        }

        var missingIds = await serviceRepository.FilterMissingIdsAsync(
            directoriesById.Keys.ToList(), CancellationToken.None);

        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(current.GracePeriodHours);
        var deletedCount = 0;

        foreach (var missingId in missingIds)
        {
            var dir = directoriesById[missingId];
            var lastWriteUtc = Directory.GetLastWriteTimeUtc(dir);

            if (lastWriteUtc > cutoff)
            {
                logger.LogInformation(
                    "Skipping dangling repository '{ServiceId}': within grace period (last write {LastWrite})",
                    missingId, lastWriteUtc);
                continue;
            }

            if (current.DryRun)
            {
                logger.LogInformation("[DryRun] Would delete dangling repository directory '{Path}'", dir);
                continue;
            }

            await gitService.DeleteServiceRepositoryAsync(missingId, CancellationToken.None);
            deletedCount++;
        }

        logger.LogInformation("Repository cleanup completed: {Count} dangling repository(ies) removed", deletedCount);
    }

    /// <summary>
    /// Manifest clones are shared between services, so one is dangling only when no service's
    /// binding resolves to it any more.
    /// </summary>
    private async Task CleanupManifestRepositoriesAsync(RepositoryCleanupOptions current)
    {
        var manifestsRoot = Path.Combine(pathProvider.GetRepositoryRootPath(), "manifests");
        if (!Directory.Exists(manifestsRoot))
            return;

        var directories = Directory.GetDirectories(manifestsRoot);
        if (directories.Length == 0)
            return;

        var inUse = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var service in serviceRepository.GetAsync(CancellationToken.None))
        {
            if (service.RepositoryBinding is { } binding)
                inUse.Add(Path.GetFullPath(pathProvider.GetManifestRepositoryPath(binding)));
        }

        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(current.GracePeriodHours);
        var deletedCount = 0;

        foreach (var dir in directories)
        {
            if (inUse.Contains(Path.GetFullPath(dir)))
                continue;

            if (Directory.GetLastWriteTimeUtc(dir) > cutoff)
            {
                logger.LogInformation("Skipping unused manifest repository '{Path}': within grace period", dir);
                continue;
            }

            if (current.DryRun)
            {
                logger.LogInformation("[DryRun] Would delete unused manifest repository directory '{Path}'", dir);
                continue;
            }

            try
            {
                Directory.Delete(dir, recursive: true);
                deletedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete unused manifest repository '{Path}'", dir);
            }
        }

        logger.LogInformation("Manifest repository cleanup completed: {Count} unused repository(ies) removed", deletedCount);
    }
}
