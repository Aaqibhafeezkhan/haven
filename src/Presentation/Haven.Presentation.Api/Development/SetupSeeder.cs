using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Configuration;
using Haven.Application.Features.Auth.Commands.InitialSetupCommand;
using Haven.Application.Features.Setup.Commands.ConfigureInstanceCommand;
using Haven.Application.Features.Setup.Commands.ConfigureNetworkCommand;

using Mediator;

namespace Haven.Presentation.Api.Development;

/// <summary>
/// Walks the first-time setup flow (instance → super user → network) with fixed dev credentials so
/// a fresh database is immediately usable. Resumes from whatever stage the instance is at, and
/// does nothing once setup is completed.
/// </summary>
public class SetupSeeder : IDevelopmentSeeder
{
    public const string AdminName = "Dev Admin";
    public const string AdminEmail = "admin@haven.dev";
    public const string AdminPassword = "haven123";

    public int Order => 0;

    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var mediator = services.GetRequiredService<IMediator>();
        var havenService = services.GetRequiredService<IHavenService>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<SetupSeeder>();

        if (await havenService.GetSetupStageAsync(cancellationToken) == SetupStage.NotStarted)
        {
            Ensure(await mediator.Send(new ConfigureInstanceCommand { InstanceName = "Haven Dev" }, cancellationToken));
            logger.LogInformation("Configured dev instance");
        }

        if (await havenService.GetSetupStageAsync(cancellationToken) == SetupStage.InstanceConfigured)
        {
            Ensure(await mediator.Send(new InitialSetupCommand
            {
                Name = AdminName,
                Email = AdminEmail,
                Password = AdminPassword
            }, cancellationToken));
            logger.LogInformation("Created dev super user. Login: {Email} / {Password}", AdminEmail, AdminPassword);
        }

        if (await havenService.GetSetupStageAsync(cancellationToken) == SetupStage.SuperUserCreated)
        {
            Ensure(await mediator.Send(new ConfigureNetworkCommand { Port = 8080 }, cancellationToken));
            logger.LogInformation("Configured dev network, first-time setup completed");
        }
    }

    private static void Ensure(Result result)
    {
        if (result.IsFailure)
            throw new InvalidOperationException($"Dev setup failed: {result.Error}");
    }
}
