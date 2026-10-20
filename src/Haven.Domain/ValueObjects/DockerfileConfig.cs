using Haven.Domain.Enums;

namespace Haven.Domain.ValueObjects;

public sealed class DockerfileConfig : ServiceSourceConfig
{
    public DockerfileSource Source { get; set; }

    public string? Repository { get; set; }
    public string? Branch { get; set; }
    public string? FilePath { get; set; }
    public string? BuildContext { get; set; }
    public Guid? GitCredentialId { get; set; }

    public string? Content { get; set; }
    public Dictionary<string, string> BuildArgs { get; set; } = [];
    public List<string> Ports { get; set; } = [];
    public List<string> CommandArgs { get; set; } = [];
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.UnlessStopped;

    /// <summary>The git repository this Dockerfile is built from, or null for a raw-content source.</summary>
    public RepositoryBinding? ToRepositoryBinding() =>
        Source == DockerfileSource.Git
            ? new RepositoryBinding { GitCredentialId = GitCredentialId, Repository = Repository, Branch = Branch, FilePath = FilePath }
            : null;
}