using Haven.Application.Common;

using YamlDotNet.Serialization;

namespace Haven.Application.Features.Services;

/// <summary>Checks a repository manifest must pass before it is imported or synced onto a service.</summary>
public static class RepositoryManifestRules
{
    // Fields that, when omitted, would silently fall back to an enum default (DockerImage / None)
    // and rewrite the service, so a repo manifest must state them explicitly.
    private static readonly string[] RequiredKeys = ["type", "exposureMode"];

    public static Result ValidateRequiredKeys(string yaml)
    {
        try
        {
            var declaredKeys = new DeserializerBuilder().Build().Deserialize<Dictionary<string, object?>>(yaml)
                               ?? throw new InvalidOperationException("The manifest is empty.");
            var missing = RequiredKeys.Where(k => !declaredKeys.Keys.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
            return missing.Count > 0
                ? Error.Validation($"The repository manifest must declare: {string.Join(", ", missing)}.")
                : Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error.Validation($"Invalid repository manifest YAML: {ex.Message}");
        }
    }
}