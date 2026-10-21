using System.Text.Json;

using Haven.Application.Common.Contracts.Notifications;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Features.NotificationChannels.Commands.CreateNotificationChannelConfig;
using Haven.Domain.Enums;

using Mediator;

namespace Haven.Presentation.Api.Development;

/// <summary>
/// Seeds notification channels pointing at harmless public echo endpoints, so notification rules
/// can be exercised end to end without real credentials. A channel is skipped if one with the
/// same name already exists.
/// </summary>
public class NotificationChannelSeeder : IDevelopmentSeeder
{
    public const string HttpbinWebhookName = "httpbin webhook";

    public int Order => 20;

    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var mediator = services.GetRequiredService<IMediator>();
        var channels = services.GetRequiredService<INotificationChannelConfigRepository>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<NotificationChannelSeeder>();

        var webhookConfig = new WebhookNotificationConfig
        {
            Url = "https://httpbin.org/post",
            Headers = new Dictionary<string, string> { ["X-Haven-Source"] = "dev-seeder" }
        };

        await EnsureChannelAsync(mediator, channels, logger, HttpbinWebhookName, NotificationChannel.Webhook,
            JsonSerializer.Serialize(webhookConfig), cancellationToken);
    }

    private static async Task EnsureChannelAsync(IMediator mediator, INotificationChannelConfigRepository channels,
        ILogger logger, string name, NotificationChannel channel, string configJson, CancellationToken ct)
    {
        var existing = await channels.GetPagedAsync(1, 10, search: name, cancellationToken: ct);
        if (existing.Items.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogInformation("Notification channel '{Channel}' already exists, skipping", name);
            return;
        }

        var result = await mediator.Send(new CreateNotificationChannelConfigCommand
        {
            Name = name,
            Channel = channel,
            ConfigJson = configJson,
            Enabled = true
        }, ct);

        if (result.IsFailure)
            throw new InvalidOperationException($"Dev seed of notification channel '{name}' failed: {result.Error}");

        logger.LogInformation("Seeded notification channel '{Channel}'", name);
    }
}