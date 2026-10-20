using Haven.Domain.ValueObjects;

namespace Haven.Application.Features.Services;

/// <summary>Where a repository-owned service takes its definition from.</summary>
public sealed class RepositoryBindingDto
{
    public Guid? GitCredentialId { get; set; }
    public string? Repository { get; set; }
    public string? Branch { get; set; }
    public string? FilePath { get; set; }

    public static RepositoryBindingDto? From(RepositoryBinding? binding) =>
        binding is null
            ? null
            : new RepositoryBindingDto
            {
                GitCredentialId = binding.GitCredentialId,
                Repository = binding.Repository,
                Branch = binding.Branch,
                FilePath = binding.FilePath
            };
}