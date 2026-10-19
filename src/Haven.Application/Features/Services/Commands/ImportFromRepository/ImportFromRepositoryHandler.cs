using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Messaging;
using Haven.Application.Mappers;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;

using Environment = Haven.Domain.Aggregates.Environment;

namespace Haven.Application.Features.Services.Commands.ImportFromRepository;

public sealed class ImportFromRepositoryHandler(
    IGitService gitService,
    IManifestParser<ServiceManifestDto> parser,
    IEnvironmentRepository environmentRepository,
    IServiceRepository serviceRepository) : ICommandHandler<ImportFromRepositoryCommand, Guid>
{
    public async ValueTask<Result<Guid>> Handle(ImportFromRepositoryCommand command, CancellationToken cancellationToken)
    {
        var environment = await environmentRepository.GetByIdAsync(command.EnvironmentId, cancellationToken);
        if (environment is null) return Error.NotFoundFor(nameof(Environment), command.EnvironmentId);

        var binding = new RepositoryBinding
        {
            GitCredentialId = command.GitCredentialId,
            Repository = command.Repository.Trim(),
            Branch = command.Branch.Trim(),
            FilePath = command.FilePath.Trim()
        };

        // The manifest clone is keyed by service id, so the id is fixed before the file is read and
        // the first deploy reuses that clone.
        var serviceId = Guid.CreateVersion7();

        var fileResult = await gitService.ReadBoundFileAsync(serviceId, binding, cancellationToken);
        if (fileResult.IsFailure) return fileResult.Error;

        ServiceManifestDto manifest;
        try
        {
            manifest = await parser.ParseAsync(fileResult.Value, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error.Validation($"Invalid repository manifest YAML: {ex.Message}");
        }

        // Only the declarative definition comes from the repo; identity, secrets, timestamps and
        // operator intent are Haven's own (see RepositoryManifestApplier).
        var now = DateTime.UtcNow;
        manifest.Id = serviceId;
        manifest.DesiredState = DesiredState.Stopped;
        manifest.Status = null;
        manifest.CreatedAt = now;
        manifest.UpdatedAt = now;
        manifest.CustomActions = [];
        if (manifest.SourceConfig is not null)
            manifest.SourceConfig.GitCredentialId = null;
        foreach (var volume in manifest.Volumes)
            volume.Id = Guid.CreateVersion7();

        var service = manifest.ToEntity(environment);
        service.RegenerateToken();
        service.RepositoryBinding = binding;

        var canCreate = await serviceRepository.CanCreateAsync(service, cancellationToken);
        if (!canCreate.IsSuccess) return canCreate.Error;

        await serviceRepository.AddAsync(service, cancellationToken);

        return service.Id;
    }
}
