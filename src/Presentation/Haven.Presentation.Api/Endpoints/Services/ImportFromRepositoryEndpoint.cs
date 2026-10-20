using FastEndpoints;

using Haven.Application.Common.Responses;
using Haven.Application.Features.Services.Commands.ImportFromRepository;
using Haven.Presentation.Api.Extensions;

using Mediator;

namespace Haven.Presentation.Api.Endpoints.Services;

public class ImportFromRepositoryEndpoint(IMediator mediator) : Endpoint<ImportFromRepositoryCommand, ApiResponse<Guid>>
{
    public override void Configure()
    {
        Post("services/import-from-repository");
    }

    public override async Task HandleAsync(ImportFromRepositoryCommand req, CancellationToken ct)
    {
        var result = await mediator.Send(req, ct);
        await this.SendResultAsync(result, ct);
    }
}