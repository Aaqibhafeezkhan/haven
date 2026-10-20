using Haven.Application.Common;
using Haven.Application.Common.Messaging;
using Haven.Domain;

namespace Haven.Application.Features.Services.Commands.ImportFromRepository;

/// <summary>
/// Creates a repository-owned service: the service definition is read from a manifest file in a git
/// repository and re-applied from there on every deploy.
/// </summary>
[RequirePermission(Permissions.ProjectManagement.Create)]
public sealed class ImportFromRepositoryCommand : ICommand<Guid>, IMutatesManifestState
{
    public Guid EnvironmentId { get; set; }
    public Guid? GitCredentialId { get; set; }
    public string Repository { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}