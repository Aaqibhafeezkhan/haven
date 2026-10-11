using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Messaging;
using Haven.Application.Features.CustomActions.Abstractions;
using Haven.Domain.Entities;

namespace Haven.Application.Features.CustomActions.Commands.ExecuteCustomAction;

public sealed class ExecuteCustomActionHandler(
    ICustomActionRepository repository,
    IActionRunner runner,
    ICurrentUserService currentUserService,
    IPermissionRepository permissionRepository)
    : ICommandHandler<ExecuteCustomActionCommand>
{
    public async ValueTask<Result> Handle(ExecuteCustomActionCommand command, CancellationToken cancellationToken)
    {
        var byToken = !string.IsNullOrWhiteSpace(command.Token);

        var action = byToken
            ? await repository.GetByTokenAsync(command.Token, cancellationToken)
            : await repository.GetByIdAsync(command.ActionId, cancellationToken);

        if (action is null || (!byToken && action.ServiceId != command.ServiceId))
            return Error.NotFoundFor(nameof(CustomAction), command.ActionId);

        await PermissionGuard.EnsureAsync(currentUserService, permissionRepository, action.RequiredPermissions,
            cancellationToken);

        return await runner.RunActionAsync(action.Id, command.Inputs, cancellationToken);
    }
}