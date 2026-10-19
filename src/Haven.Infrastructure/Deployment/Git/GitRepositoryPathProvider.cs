using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Haven.Application.Common.Interfaces.Deployment;
using Haven.Domain.ValueObjects;

namespace Haven.Infrastructure.Deployment.Git;

public sealed class GitRepositoryPathProvider(string baseRepositoryPath) : IGitRepositoryPathProvider
{
    private readonly string _baseRepositoryPath = baseRepositoryPath.TrimEnd(Path.DirectorySeparatorChar);

    public string GetRepositoryRootPath() => _baseRepositoryPath;

    public string GetRepositoryPath(RepositoryBinding binding)
    {
        var repository = NormalizeRepository(binding.Repository);
        var branch = string.IsNullOrWhiteSpace(binding.Branch) ? "main" : binding.Branch.Trim();
        var credential = binding.GitCredentialId?.ToString() ?? "none";

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{repository}\n{branch}\n{credential}")));

        var name = Regex.Replace(repository[(repository.LastIndexOfAny(['/', ':']) + 1)..], "[^a-z0-9-]+", "-").Trim('-');
        if (name.Length > 40) name = name[..40];
        if (name.Length == 0) name = "repo";

        return Path.Combine(_baseRepositoryPath, "repositories", $"{name}-{hash[..16]}");
    }

    public bool RepositoryDirectoryExists(RepositoryBinding binding) =>
        Directory.Exists(GetRepositoryPath(binding));

    // So "https://host/o/r", "https://host/o/r.git" and "https://host/o/r/" map to the same clone.
    private static string NormalizeRepository(string? repository)
    {
        var value = (repository ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
        return value.EndsWith(".git", StringComparison.Ordinal) ? value[..^4] : value;
    }
}
