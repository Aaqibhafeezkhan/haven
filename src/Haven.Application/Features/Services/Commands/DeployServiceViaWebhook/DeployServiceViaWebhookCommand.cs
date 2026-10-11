using Haven.Application.Common;
using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.Services.Commands.DeployServiceViaWebhook;

[RequirePermission(Permissions.ProjectManagement.ManageDeploys)]
public sealed class DeployServiceViaWebhookCommand : ICommand
{
    public string Token { get; set; } = string.Empty;
}