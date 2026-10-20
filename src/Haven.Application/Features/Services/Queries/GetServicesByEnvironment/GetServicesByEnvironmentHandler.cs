using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;
using Haven.Application.Common.Messaging;
using Haven.Domain.Aggregates;


namespace Haven.Application.Features.Services.Queries.GetServicesByEnvironment;

public sealed class GetServicesByEnvironmentHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IServiceRepository serviceRepository,
    IServiceStateReader stateReader)
    : IQueryHandler<GetServicesByEnvironmentQuery, IReadOnlyList<ServiceDto>>
{
    public async ValueTask<Result<IReadOnlyList<ServiceDto>>> Handle(GetServicesByEnvironmentQuery query, CancellationToken cancellationToken)
    {
        var projectExists = await projectRepository.GetByIdAsync(query.ProjectId, cancellationToken) is not null;
        if (!projectExists)
            return Error.NotFoundFor(nameof(Project), query.ProjectId);

        var environments = await environmentRepository.GetByProjectIdAsync(query.ProjectId, cancellationToken);
        var environment = environments.FirstOrDefault(e => e.Id == query.EnvironmentId);
        if (environment is null)
            return Error.NotFoundFor("Environment", query.EnvironmentId);

        var services = await serviceRepository.GetByEnvironmentIdAsync(query.EnvironmentId, cancellationToken);

        var state = await stateReader.LoadAsync(services.Select(s => s.Id), cancellationToken);

        var items = services
            .Select(s => new ServiceDto(s.Id, s.EnvironmentId, s.Name, s.Type, s.ExposureMode, state.GetDisplayStatus(s.Id), s.SourceConfig, s.CreatedAt, s.UpdatedAt)
            {
                DesiredState = s.DesiredState,
                RuntimeState = state.GetRuntimeState(s.Id),
                Health = state.GetHealth(s.Id),
                RepositoryBinding = RepositoryBindingDto.From(s.RepositoryBinding)
            })
            .ToList();

        return Result<IReadOnlyList<ServiceDto>>.Success(items);
    }
}