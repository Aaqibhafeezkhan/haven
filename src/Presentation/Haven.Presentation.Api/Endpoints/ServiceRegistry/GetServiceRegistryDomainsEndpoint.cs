using FastEndpoints;

using Haven.Application.Common.Messaging;
using Haven.Application.Features.ServiceRegistry.Queries.GetServiceRegistryDomains;
using Haven.Presentation.Api.Extensions;

using Mediator;

namespace Haven.Presentation.Api.Endpoints.ServiceRegistry;

public sealed class GetServiceRegistryDomainsEndpoint(IMediator mediator)
    : Endpoint<GetServiceRegistryDomainsQuery, PagedResult<ServiceRegistryDomainListItemDto>>
{
    public override void Configure()
    {
        Get("/service-registry/domains");

        Options(x => x.WithTags("ServiceRegistry"));
        Summary(s =>
        {
            s.Summary = "List all registry domains";
            s.Description = "Returns a paginated list of every domain across all services and sidecars, with the owning service and project.";
            s[200] = "OK";
            s[204] = "No content";
        });
    }

    public override async Task HandleAsync(GetServiceRegistryDomainsQuery req, CancellationToken ct)
    {
        var result = await mediator.Send(req, ct);
        await this.SendResultAsync(result, ct);
    }
}
