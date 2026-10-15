using Haven.Application.Common;
using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.ServiceRegistry.Queries.GetServiceRegistrySummary;

[RequirePermission(Permissions.ProjectManagement.Read)]
public sealed class GetServiceRegistrySummaryQuery : IQuery<ServiceRegistrySummaryDto>;
