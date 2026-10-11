using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.ApiTokens.Commands;

public sealed class RegenerateApiTokenCommand : ICommand<GenerateApiTokenResult>
{
    public Guid Id { get; set; }
}