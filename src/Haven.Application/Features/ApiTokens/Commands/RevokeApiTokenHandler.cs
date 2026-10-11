using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Messaging;
using Haven.Application.Features.ApiTokens.Abstractions;

namespace Haven.Application.Features.ApiTokens.Commands;

public sealed class RevokeApiTokenHandler(
    IApiTokenRepository repository,
    IApiTokenService service,
    ICurrentUserService currentUserService) : ICommandHandler<RevokeApiTokenCommand>
{
    public async ValueTask<Result> Handle(RevokeApiTokenCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Error.Unauthorized;

        var token = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (token is null || token.OwnerId != userId.Value)
            return Error.NotFoundFor("API token", command.Id);

        if (token.RevokedAt is not null)
            return Result.Success();

        await service.RevokeAsync(token.Id, cancellationToken);

        return Result.Success();
    }
}