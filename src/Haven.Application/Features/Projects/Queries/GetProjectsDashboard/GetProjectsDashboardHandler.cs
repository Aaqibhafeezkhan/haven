using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;
using Haven.Application.Common.Messaging;
using Haven.Application.Mappers;

namespace Haven.Application.Features.Projects.Queries.GetProjectsDashboard;

public sealed class GetProjectsDashboardHandler(IProjectRepository repository, IServiceStateReader stateReader)
    : IPagedQueryHandler<GetProjectsDashboardQuery, ProjectDashboardDto>
{
    public async ValueTask<PagedResult<ProjectDashboardDto>> Handle(GetProjectsDashboardQuery query, CancellationToken cancellationToken)
    {
        var paged = await repository.GetPagedAsync(query.PageNumber, query.PageSize, cancellationToken);

        var serviceIds = paged.Items.SelectMany(p => p.Environments).SelectMany(e => e.Services).Select(s => s.Id);
        var state = await stateReader.LoadAsync(serviceIds, cancellationToken);

        return paged.Project(project => project.ToDashboardDto(state: state));
    }
}