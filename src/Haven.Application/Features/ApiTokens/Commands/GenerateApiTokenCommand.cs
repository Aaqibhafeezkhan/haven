using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.ApiTokens.Commands;

public sealed class GenerateApiTokenCommand : ICommand<GenerateApiTokenResult>
{
    public string Name { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public TimeSpan? Expiration { get; set; }
}