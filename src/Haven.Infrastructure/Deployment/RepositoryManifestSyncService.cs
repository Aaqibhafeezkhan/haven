using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Features.Services;
using Haven.Domain.Aggregates;
using Haven.Domain.Exceptions;

using Microsoft.Extensions.Logging;

using YamlDotNet.Serialization;

namespace Haven.Infrastructure.Deployment;

public class RepositoryManifestSyncService(
    IGitService gitService,
    IManifestParser<ServiceManifestDto> manifestParser,
    ILogger<RepositoryManifestSyncService> logger) : IRepositoryManifestSyncService
{
    // Fields that, when omitted, would silently fall back to an enum default (DockerImage / None)
    // and rewrite the service, so a repo manifest must state them explicitly.
    private static readonly string[] RequiredKeys = ["type", "exposureMode"];

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

        ServiceManifestDto manifest;
        try
        {
            var declaredKeys = new DeserializerBuilder().Build().Deserialize<Dictionary<string, object?>>(fileResult.Value)
                               ?? throw new InvalidOperationException("The manifest is empty.");
            var missing = RequiredKeys.Where(k => !declaredKeys.Keys.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missing.Count > 0)
                return Error.Validation($"The repository manifest must declare: {string.Join(", ", missing)}.");

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
