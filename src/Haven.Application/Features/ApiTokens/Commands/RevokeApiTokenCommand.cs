using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.ApiTokens.Commands;

public class RevokeApiTokenCommand : ICommand
{
    public Guid Id { get; set; }
}
