using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Messaging;
using Haven.Application.Features.ApiTokens.Abstractions;

namespace Haven.Application.Features.ApiTokens.Commands;

public class GenerateApiTokenHandler(IApiTokenService service, ICurrentUserService currentUserService) : ICommandHandler<GenerateApiTokenCommand, GenerateApiTokenResult>
{
    public async ValueTask<Result<GenerateApiTokenResult>> Handle(GenerateApiTokenCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId is null)
            return Error.Unauthorized;
        
        // Only a signed-in admin can delegate admin privilege (IsAdmin is never true for API tokens,
        // so a token can't mint another admin token).
        if (command.Scopes.Contains(Permissions.AdminScope) && !currentUserService.IsAdmin)
            return Error.Forbidden;

        var (token, plainText) = await service.CreateAsync(
            ownerId: userId.Value,
            name: command.Name,
            scopes: command.Scopes,
            expiration: command.Expiration,
            cancellationToken: cancellationToken);

        return new GenerateApiTokenResult() { PlainText = plainText };
    }
}