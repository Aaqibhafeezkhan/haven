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
        var repository = binding.NormalizedRepository();
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(binding.CloneKey())));

        var name = Regex.Replace(repository[(repository.LastIndexOfAny(['/', ':']) + 1)..], "[^a-z0-9-]+", "-").Trim('-');
        if (name.Length > 40) name = name[..40];
        if (name.Length == 0) name = "repo";

        return Path.Combine(_baseRepositoryPath, "repositories", $"{name}-{hash[..16]}");
    }

    public bool RepositoryDirectoryExists(RepositoryBinding binding) =>
        Directory.Exists(GetRepositoryPath(binding));
}