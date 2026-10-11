using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Messaging;
using Haven.Application.Features.ApiTokens.Abstractions;

namespace Haven.Application.Features.ApiTokens.Commands;

public class RegenerateApiTokenHandler(
    IApiTokenRepository repository,
    IApiTokenService service,
    ICurrentUserService currentUserService) : ICommandHandler<RegenerateApiTokenCommand, GenerateApiTokenResult>
{
    public async ValueTask<Result<GenerateApiTokenResult>> Handle(RegenerateApiTokenCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Error.Unauthorized;

        var token = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (token is null || token.OwnerId != userId.Value)
            return Error.NotFoundFor("API token", command.Id);

        if (token.RevokedAt is not null)
            return Error.InvalidOperation("A revoked API token cannot be regenerated.");

        var plainText = await service.RegenerateAsync(token.Id, cancellationToken);

        return new GenerateApiTokenResult { PlainText = plainText };
    }
}
