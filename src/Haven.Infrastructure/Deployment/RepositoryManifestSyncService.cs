using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Features.Services;
using Haven.Domain.Aggregates;
using Haven.Domain.Exceptions;

using Microsoft.Extensions.Logging;

namespace Haven.Infrastructure.Deployment;

public class RepositoryManifestSyncService(
    IGitService gitService,
    IManifestParser<ServiceManifestDto> manifestParser,
    ILogger<RepositoryManifestSyncService> logger) : IRepositoryManifestSyncService
{
    public async Task<Result<bool>> SyncAsync(Service service, CancellationToken cancellationToken)
    {
        if (service.RepositoryBinding is not { } binding)
            return false;

        var fileResult = await gitService.ReadBoundFileAsync(binding, cancellationToken);
        if (fileResult.IsFailure)
        {
            logger.LogWarning("Could not read manifest '{FilePath}' from '{Repository}' for service '{Name}': {Error}",
                binding.FilePath, binding.Repository, service.Name, fileResult.Error.Message);
            return fileResult.Error;
        }

        var keysResult = RepositoryManifestRules.ValidateRequiredKeys(fileResult.Value);
        if (keysResult.IsFailure) return keysResult.Error;

        ServiceManifestDto manifest;
        try
        {
            manifest = await manifestParser.ParseAsync(fileResult.Value, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error.Validation($"Invalid repository manifest YAML: {ex.Message}");
        }

        if (manifest.CustomActions.Count > 0)
            logger.LogWarning("Repository manifest for service '{Name}' declares custom actions, which are not synced from repositories and were ignored", service.Name);

        try
        {
            var changed = RepositoryManifestApplier.Apply(service, manifest);
            if (changed)
                logger.LogInformation("Service '{Name}' updated from repository manifest '{FilePath}'", service.Name, binding.FilePath);
            return changed;
        }
        catch (HavenException ex)
        {
            return Error.Validation($"The repository manifest is not valid for this service: {ex.Message}");
        }
    }
}
