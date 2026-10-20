using System.Text.Json;

using Haven.Application.Mappers;
using Haven.Domain;
using Haven.Domain.Aggregates;
using Haven.Domain.ValueObjects;

namespace Haven.Application.Features.Services;

/// <summary>
/// Applies a manifest that lives in a git repository onto a repository-owned <see cref="Service"/>.
/// <para>
/// The repo file only carries the declarative definition of the service. These are deliberately
/// never taken from it, so the same file can be committed once and applied to any environment
/// (think kustomize base vs. cluster state):
/// <list type="bullet">
/// <item><description>Identity: <c>id</c>, <c>name</c>, <c>alias</c>, and the ids of nested volumes/actions/flags (Haven names its Docker resources after these).</description></item>
/// <item><description>Secrets: <c>token</c>, and custom actions (they carry their own tokens/permissions).</description></item>
/// <item><description>Bookkeeping: <c>createdAt</c>, <c>updatedAt</c>.</description></item>
/// <item><description>Operator intent: <c>desiredState</c>/<c>status</c>; running or stopping is decided by deploy/stop, not by the repo.</description></item>
/// <item><description>Git credentials: a Dockerfile source's <c>gitCredentialId</c> is a Haven-local id and is kept from the existing service.</description></item>
/// </list>
/// </para>
/// Feature flags are matched by name and volumes by mount target; anything the service has that the
/// manifest no longer declares is removed.
/// </summary>
public static class RepositoryManifestApplier
{
    /// <summary>Returns whether the service changed.</summary>
    public static bool Apply(Service service, ServiceManifestDto manifest)
    {
        var changed = ApplyDefinition(service, manifest);
        changed |= ApplyFeatureFlags(service, manifest);
        changed |= ApplyVolumes(service, manifest);
        return changed;
    }

    private static bool ApplyDefinition(Service service, ServiceManifestDto manifest)
    {
        Optional<ServiceSourceConfig?> sourceConfig = default;

        var desired = manifest.SourceConfig.ToDomain(manifest.Type);
        if (desired is not null)
        {
            if (desired is DockerfileConfig dockerfile)
                dockerfile.GitCredentialId = (service.SourceConfig as DockerfileConfig)?.GitCredentialId;

            // Service.Update treats any provided source config as a change, so only pass a real difference.
            if (service.SourceConfigJson != JsonSerializer.Serialize<ServiceSourceConfig>(desired))
                sourceConfig = desired;
        }

        var changed = service.Update(default, manifest.Type, manifest.ExposureMode, default, sourceConfig);

        // The repo is the source of truth: a source config it no longer declares must not linger and
        // contradict the (possibly changed) service type.
        if (desired is null)
            changed |= service.ClearSourceConfig();

        return changed;
    }

    private static bool ApplyFeatureFlags(Service service, ServiceManifestDto manifest)
    {
        var changed = false;
        var declared = manifest.FeatureFlags.DistinctBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var stale in service.FeatureFlags
                     .Where(f => !declared.Any(d => string.Equals(d.Name, f.Name, StringComparison.OrdinalIgnoreCase)))
                     .ToList())
        {
            service.RemoveFeatureFlag(stale);
            changed = true;
        }

        foreach (var flag in declared)
        {
            var existing = service.FeatureFlags.FirstOrDefault(f => string.Equals(f.Name, flag.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                service.AddFeatureFlag(flag.Name, flag.Type, flag.Key, flag.Description, flag.Value, flag.ValueType);
                changed = true;
                continue;
            }

            if (existing.Type == flag.Type && existing.Key == flag.Key && existing.Description == flag.Description
                && existing.Value == flag.Value && existing.ValueType == flag.ValueType)
                continue;

            existing.Type = flag.Type;
            existing.Key = flag.Key;
            existing.Description = flag.Description;
            existing.Value = flag.Value;
            existing.ValueType = flag.ValueType;
            changed = true;
        }

        return changed;
    }

    private static bool ApplyVolumes(Service service, ServiceManifestDto manifest)
    {
        var changed = false;
        var declared = manifest.Volumes.DistinctBy(v => v.Target.Trim()).ToList();

        // A volume whose type changes can't be edited in place, so it is recreated.
        var stale = service.Volumes
            .Where(v => declared.FirstOrDefault(d => d.Target.Trim() == v.Target) is not { } match || match.Type != v.Type)
            .ToList();

        foreach (var volume in stale)
        {
            service.RemoveVolume(volume);
            changed = true;
        }

        foreach (var volume in declared)
        {
            var target = volume.Target.Trim();
            var existing = service.Volumes.FirstOrDefault(v => v.Target == target);
            if (existing is null)
            {
                service.AddVolume(volume.Type, volume.Name, target, volume.Source, volume.ReadOnly, volume.BackupEnabled);
                changed = true;
                continue;
            }

            if (existing.Name == volume.Name && existing.Source == volume.Source
                && existing.ReadOnly == volume.ReadOnly && existing.BackupEnabled == volume.BackupEnabled)
                continue;

            service.UpdateVolume(existing, volume.Name, volume.Source, default, volume.ReadOnly, volume.BackupEnabled);
            changed = true;
        }

        return changed;
    }
}