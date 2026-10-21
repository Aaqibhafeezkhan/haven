using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Messaging;
using Haven.Application.Features.Environments.Commands.CreateEnvironment;
using Haven.Application.Features.EnvironmentVariables.Commands.SetEnvForEnvironment;
using Haven.Application.Features.EnvironmentVariables.Commands.SetEnvForProject;
using Haven.Application.Features.EnvironmentVariables.Commands.SetEnvForService;
using Haven.Application.Features.FeatureFlags.Commands.CreateFeatureFlagCommand;
using Haven.Application.Features.HealthChecks.Commands.CreateHealthCheckCommand;
using Haven.Application.Features.Projects.Commands.CreateProject;
using Haven.Application.Features.Secrets.Commands.CreateSecretVariable;
using Haven.Application.Features.Services.Commands.AddVolume;
using Haven.Application.Features.Services.Commands.CreateService;
using Haven.Application.Features.Services.Commands.WriteVolumeFileContent;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;

using Mediator;

namespace Haven.Presentation.Api.Development;

/// <summary>
/// Seeds fake multi-environment service stacks built from lightweight public images (nginx,
/// whoami, redis, ...). Goes through the mediator so validation, domain events and manifest sync
/// behave exactly as they do for real requests. A stack is skipped if its project already exists.
/// </summary>
public class ProjectStackSeeder : IDevelopmentSeeder
{
    public int Order => 10;

    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        await SeedStacksAsync(services, Stacks, services.GetRequiredService<ILoggerFactory>().CreateLogger<ProjectStackSeeder>(), cancellationToken);

    /// <summary>
    /// Seeds each stack whose project doesn't exist yet. Shared with other seeders that only
    /// differ in the stacks they define.
    /// </summary>
    internal static async Task SeedStacksAsync(IServiceProvider services, IEnumerable<ProjectStack> stacks, ILogger logger, CancellationToken cancellationToken)
    {
        var mediator = services.GetRequiredService<IMediator>();
        var projects = services.GetRequiredService<IProjectRepository>();

        foreach (var stack in stacks)
        {
            if (await projects.ExistsWithNameAsync(stack.Name, Guid.Empty, cancellationToken))
            {
                logger.LogInformation("Project '{Project}' already exists, skipping stack", stack.Name);
                continue;
            }

            await SeedStackAsync(mediator, stack, cancellationToken);
            logger.LogInformation("Seeded project stack '{Project}'", stack.Name);
        }
    }

    private static async Task SeedStackAsync(IMediator mediator, ProjectStack stack, CancellationToken ct)
    {
        var projectId = await Send(mediator, new CreateProjectCommand
        {
            Name = stack.Name,
            Alias = stack.Alias,
            Description = stack.Description
        }, ct);

        if (stack.Env.Count > 0)
            await Send(mediator, new SetEnvForProjectCommand { ProjectId = projectId, EnvFile = ToEnvFile(stack.Env) }, ct);

        foreach (var (key, value) in stack.Secrets)
            await Send(mediator, new CreateSecretVariableCommand
            {
                ParentId = projectId,
                ParentType = EnvironmentVariableParentType.Project,
                Key = key,
                Value = value
            }, ct);

        foreach (var env in stack.Environments)
        {
            var environmentId = await Send(mediator, new CreateEnvironmentCommand
            {
                ProjectId = projectId,
                Name = env.Name,
                Alias = env.Alias,
                Description = env.Description
            }, ct);

            if (env.Env.Count > 0)
                await Send(mediator, new SetEnvForEnvironmentCommand
                {
                    ProjectId = projectId,
                    EnvironmentId = environmentId,
                    EnvFile = ToEnvFile(env.Env)
                }, ct);

            foreach (var (key, value) in env.Secrets)
                await Send(mediator, new CreateSecretVariableCommand
                {
                    ParentId = environmentId,
                    ParentType = EnvironmentVariableParentType.Environment,
                    Key = key,
                    Value = value
                }, ct);

            foreach (var service in env.Services)
                await SeedServiceAsync(mediator, projectId, environmentId, service, ct);
        }
    }

    private static async Task SeedServiceAsync(IMediator mediator, Guid projectId, Guid environmentId, ServiceDef def, CancellationToken ct)
    {
        var serviceId = await Send(mediator, new CreateServiceCommand
        {
            ProjectId = projectId,
            EnvironmentId = environmentId,
            Name = def.Name,
            Alias = def.Alias,
            Type = ServiceType.DockerImage,
            ExposureMode = def.Exposure,
            DockerConfig = new DockerConfig
            {
                Image = def.Image,
                Ports = [.. def.Ports],
                CommandArgs = [.. def.Args],
                RestartPolicy = def.RestartPolicy
            }
        }, ct);

        if (def.Env.Count > 0)
            await Send(mediator, new SetEnvForServiceCommand
            {
                ProjectId = projectId,
                EnvironmentId = environmentId,
                ServiceId = serviceId,
                EnvFile = ToEnvFile(def.Env)
            }, ct);

        foreach (var (key, value) in def.Secrets)
            await Send(mediator, new CreateSecretVariableCommand
            {
                ParentId = serviceId,
                ParentType = EnvironmentVariableParentType.Service,
                Key = key,
                Value = value
            }, ct);

        foreach (var volume in def.Volumes)
        {
            var volumeId = await Send(mediator, new AddVolumeCommand
            {
                ServiceId = serviceId,
                Type = volume.Type,
                Name = volume.Name,
                Target = volume.Target,
                Source = volume.Source,
                ReadOnly = volume.ReadOnly,
                BackupEnabled = volume.BackupEnabled
            }, ct);

            // Files can only be authored while the volume is writable, so read-only volumes stay empty.
            foreach (var (path, content) in volume.Files)
                await Send(mediator, new WriteVolumeFileContentCommand
                {
                    ServiceId = serviceId,
                    VolumeId = volumeId,
                    Path = path,
                    Content = content
                }, ct);
        }

        foreach (var flag in def.Flags)
            await Send(mediator, new CreateFeatureFlagCommand
            {
                ServiceId = serviceId,
                Name = flag.Name,
                Type = FeatureFlagType.EnvironmentVariable,
                Key = flag.Key,
                Description = flag.Description,
                Value = flag.Value,
                ValueType = flag.ValueType
            }, ct);

        foreach (var check in def.HealthChecks)
            await Send(mediator, new CreateHealthCheckCommand
            {
                ServiceId = serviceId,
                Name = check.Name,
                Kind = check.Kind,
                CronExpression = check.Cron,
                Config = check.Config
            }, ct);
    }

    /// <summary>
    /// Sends a command and unwraps the created id; a failed result aborts the seed with the error
    /// so a half-built stack is loud rather than silent.
    /// </summary>
    private static async Task<Guid> Send(IMediator mediator, Haven.Application.Common.Messaging.ICommand<Guid> command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
            throw new InvalidOperationException($"Seeding failed on {command.GetType().Name}: {result.Error}");
        return result.Value;
    }

    private static async Task Send(IMediator mediator, Haven.Application.Common.Messaging.ICommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
            throw new InvalidOperationException($"Seeding failed on {command.GetType().Name}: {result.Error}");
    }

    private static string ToEnvFile(IReadOnlyDictionary<string, string> env) =>
        string.Join('\n', env.Select(kv => $"{kv.Key}={kv.Value}"));

    private static string SamplePage(string title) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{title}}</title>
          <style>
            body { font-family: system-ui, sans-serif; display: grid; place-items: center; min-height: 100vh; margin: 0; background: #0f172a; color: #e2e8f0; }
            main { text-align: center; }
            h1 { margin-bottom: .25rem; }
            p { color: #94a3b8; }
          </style>
        </head>
        <body>
          <main>
            <h1>{{title}}</h1>
            <p>Served by nginx from a Haven managed volume.</p>
          </main>
        </body>
        </html>
        """;

    internal static readonly IReadOnlyDictionary<string, string> NoEnv = new Dictionary<string, string>();

    private static IReadOnlyList<ProjectStack> Stacks =>
    [
        new("Project Alpha", "alpha", "Static site + cache, the basic web stack",
            Env: new Dictionary<string, string> { ["TZ"] = "UTC", ["LOG_LEVEL"] = "info" },
            Secrets: new Dictionary<string, string> { ["ALPHA_API_KEY"] = "alpha-dev-key-123" },
            Environments:
            [
                new("Development", "dev", "Development environment for Project Alpha",
                    Env: new Dictionary<string, string> { ["APP_ENV"] = "development" },
                    Secrets: NoEnv,
                    Services:
                    [
                        new("nginx", "nginx", "nginx:alpine")
                        {
                            Ports = ["8081:80"],
                            Exposure = ExposureMode.Internal,
                            Env = new Dictionary<string, string> { ["NGINX_ENTRYPOINT_QUIET_LOGS"] = "1" },
                            Volumes =
                            [
                                new(VolumeType.Managed, "nginx pages", "/usr/share/nginx/html") { Files = [("index.html", SamplePage("Project Alpha (dev)"))] },
                                new(VolumeType.Named, "nginx cache", "/var/cache/nginx", Source: "alpha-dev-nginx-cache")
                            ],
                            Flags =
                            [
                                new("Maintenance mode", "MAINTENANCE_MODE", "false", FeatureFlagValueType.Bool, "Serve the maintenance page"),
                                new("New landing page", "NEW_LANDING", "true", FeatureFlagValueType.Bool, "Roll out the redesigned landing page")
                            ],
                            HealthChecks =
                            [
                                new("Homepage", HealthCheckKind.Http, """{"url":"http://localhost:8081","method":"GET","expectedStatusCodes":[200],"timeoutSeconds":5}""", "*/1 * * * *"),
                                new("Container running", HealthCheckKind.Container, "{}")
                            ]
                        },
                        new("whoami", "whoami", "traefik/whoami:latest")
                        {
                            Ports = ["8082:80"],
                            Flags = [new("Request limit", "MAX_REQUESTS", "100", FeatureFlagValueType.Number, "Requests per minute")]
                        },
                        new("redis", "redis", "redis:alpine")
                        {
                            Volumes = [new(VolumeType.Named, "redis data", "/data", Source: "alpha-dev-redis-data", BackupEnabled: true)],
                            HealthChecks = [new("Redis ping", HealthCheckKind.Bash, """{"command":"redis-cli ping","expectedExitCode":0,"timeoutSeconds":5}""")]
                        }
                    ]),
                new("Production", "prod", "Production environment for Project Alpha",
                    Env: new Dictionary<string, string> { ["APP_ENV"] = "production" },
                    Secrets: new Dictionary<string, string> { ["SESSION_SECRET"] = "prod-session-secret" },
                    Services:
                    [
                        new("nginx", "nginx", "nginx:stable-alpine")
                        {
                            Exposure = ExposureMode.External,
                            Volumes = [new(VolumeType.Managed, "nginx pages", "/usr/share/nginx/html", BackupEnabled: true) { Files = [("index.html", SamplePage("Project Alpha (prod)"))] }],
                            Flags = [new("Maintenance mode", "MAINTENANCE_MODE", "false", FeatureFlagValueType.Bool, null)]
                        },
                        new("redis", "redis", "redis:alpine")
                        {
                            Args = ["--appendonly", "yes"],
                            Secrets = new Dictionary<string, string> { ["REDIS_PASSWORD"] = "prod-redis-password" },
                            Volumes = [new(VolumeType.Named, "redis data", "/data", Source: "alpha-prod-redis-data", BackupEnabled: true)]
                        }
                    ])
            ]),

        new("Project Beta", "beta", "API + database + mail catcher",
            Env: new Dictionary<string, string> { ["TZ"] = "America/Sao_Paulo" },
            Secrets: NoEnv,
            Environments:
            [
                new("Development", "dev", "Development environment for Project Beta",
                    Env: NoEnv,
                    Secrets: NoEnv,
                    Services:
                    [
                        new("api", "api", "traefik/whoami:latest")
                        {
                            Ports = ["8091:80"],
                            Exposure = ExposureMode.Internal,
                            Env = new Dictionary<string, string>
                            {
                                ["DATABASE_HOST"] = "postgres",
                                ["DATABASE_NAME"] = "beta"
                            },
                            Secrets = new Dictionary<string, string> { ["DATABASE_PASSWORD"] = "beta-dev-password" },
                            Flags =
                            [
                                new("Verbose logging", "VERBOSE_LOGGING", "true", FeatureFlagValueType.Bool, null),
                                new("Release channel", "RELEASE_CHANNEL", "canary", FeatureFlagValueType.String, "Which release channel to follow")
                            ],
                            HealthChecks = [new("API root", HealthCheckKind.Http, """{"url":"http://localhost:8091","method":"GET","expectedStatusCodes":[200],"timeoutSeconds":5}""", "*/5 * * * *")]
                        },
                        new("postgres", "postgres", "postgres:alpine")
                        {
                            Env = new Dictionary<string, string> { ["POSTGRES_DB"] = "beta", ["POSTGRES_USER"] = "beta" },
                            Secrets = new Dictionary<string, string> { ["POSTGRES_PASSWORD"] = "beta-dev-password" },
                            Volumes = [new(VolumeType.Named, "postgres data", "/var/lib/postgresql/data", Source: "beta-dev-postgres-data", BackupEnabled: true)],
                            HealthChecks = [new("pg_isready", HealthCheckKind.Bash, """{"command":"pg_isready -U beta","expectedExitCode":0,"timeoutSeconds":5}""", "*/2 * * * *")]
                        },
                        new("mailpit", "mailpit", "axllent/mailpit:latest")
                        {
                            Ports = ["8025:8025"],
                            Volumes = [new(VolumeType.Named, "mailpit data", "/data", Source: "beta-dev-mailpit-data")]
                        }
                    ])
            ])
    ];

    internal sealed record ProjectStack(
        string Name,
        string Alias,
        string Description,
        IReadOnlyDictionary<string, string> Env,
        IReadOnlyDictionary<string, string> Secrets,
        IReadOnlyList<EnvironmentDef> Environments);

    internal sealed record EnvironmentDef(
        string Name,
        string Alias,
        string Description,
        IReadOnlyDictionary<string, string> Env,
        IReadOnlyDictionary<string, string> Secrets,
        IReadOnlyList<ServiceDef> Services);

    internal sealed record ServiceDef(string Name, string Alias, string Image)
    {
        public IReadOnlyList<string> Ports { get; init; } = [];
        public IReadOnlyList<string> Args { get; init; } = [];
        public ExposureMode Exposure { get; init; } = ExposureMode.None;
        public RestartPolicy RestartPolicy { get; init; } = RestartPolicy.UnlessStopped;
        public IReadOnlyDictionary<string, string> Env { get; init; } = NoEnv;
        public IReadOnlyDictionary<string, string> Secrets { get; init; } = NoEnv;
        public IReadOnlyList<VolumeDef> Volumes { get; init; } = [];
        public IReadOnlyList<FlagDef> Flags { get; init; } = [];
        public IReadOnlyList<HealthCheckDef> HealthChecks { get; init; } = [];
    }

    internal sealed record VolumeDef(VolumeType Type, string Name, string Target, string? Source = null, bool ReadOnly = false, bool BackupEnabled = false)
    {
        public IReadOnlyList<(string Path, string Content)> Files { get; init; } = [];
    }

    internal sealed record FlagDef(string Name, string Key, string Value, FeatureFlagValueType ValueType, string? Description);

    internal sealed record HealthCheckDef(string Name, HealthCheckKind Kind, string Config, string? Cron = null);
}