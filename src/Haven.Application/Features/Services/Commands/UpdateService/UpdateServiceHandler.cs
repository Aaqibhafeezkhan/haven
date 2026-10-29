using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Messaging;
using Haven.Domain;
using Haven.Domain.Aggregates;
using Haven.Domain.ValueObjects;

using Environment = Haven.Domain.Aggregates.Environment;

namespace Haven.Application.Features.Services.Commands.UpdateService;

public sealed class UpdateServiceHandler(
    IProjectRepository projectRepository) : ICommandHandler<UpdateServiceCommand, Guid>
{
    public async ValueTask<Result<Guid>> Handle(UpdateServiceCommand request, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
            return Error.NotFoundFor(nameof(Project), request.ProjectId);

        var environment = project.Environments.FirstOrDefault(e => e.Id == request.EnvironmentId);
        if (environment is null)
            return Error.NotFoundFor(nameof(Environment), request.EnvironmentId);

        var service = environment.Services.FirstOrDefault(s => s.Id == request.ServiceId);
        if (service is null)
            return Error.NotFoundFor("Service", request.ServiceId);

        if (service.IsRepositoryManaged
            && (request.Type.HasValue || request.ExposureMode.HasValue || request.DockerConfig.HasValue || request.DockerfileConfig.HasValue || request.ResourceLimits.HasValue))
            return Error.RepositoryManaged("The service type, exposure mode and source configuration");

        if (request.Name.HasValue && !string.Equals(request.Name.Value, service.Name, StringComparison.OrdinalIgnoreCase))
        {
            var nameConflict = environment.Services.Any(s =>
                s.Id != request.ServiceId &&
                string.Equals(s.Name, request.Name.Value, StringComparison.OrdinalIgnoreCase));
            if (nameConflict)
                return Error.ConflictFor("Service", request.Name.Value);
        }

        var sourceConfig = request.ResolveSourceConfig();

        // Replacement configs carry no limits of their own, so keep the stored ones unless new limits were sent.
        if (sourceConfig.HasValue && sourceConfig.Value is DockerBasedSourceConfig incoming)
        {
            var limits = request.ResourceLimits.HasValue
                ? request.ResourceLimits.Value
                : service.SourceConfig is DockerBasedSourceConfig existing
                    ? new ServiceResourceLimits
                    {
                        MemoryLimit = existing.MemoryLimit,
                        MemorySwap = existing.MemorySwap,
                        MemoryReservation = existing.MemoryReservation,
                        NanoCpus = existing.NanoCpus
                    }
                    : null;
            (limits ?? new ServiceResourceLimits()).ApplyTo(incoming);
        }
        else if (!sourceConfig.HasValue && request.ResourceLimits.HasValue && service.SourceConfig is DockerBasedSourceConfig stored)
        {
            // Limits sent on their own go onto a copy of the stored config (the getter deserializes a fresh one).
            (request.ResourceLimits.Value ?? new ServiceResourceLimits()).ApplyTo(stored);
            sourceConfig = (Optional<ServiceSourceConfig?>)stored;
        }

        environment.UpdateService(request.ServiceId, request.Name, request.Type, request.ExposureMode, request.Alias, sourceConfig);

        return Result<Guid>.Success(request.ServiceId);
    }
}