namespace Haven.Presentation.Api.Development;

public static class DevelopmentSeedRunner
{
    public const string SeedArgument = "--seed";

    public static bool IsSeedRequested(string[] args) =>
        args.Contains(SeedArgument, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers every concrete <see cref="IDevelopmentSeeder"/> in this assembly.
    /// </summary>
    public static IServiceCollection AddDevelopmentSeeders(this IServiceCollection services)
    {
        var seeders = typeof(IDevelopmentSeeder).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDevelopmentSeeder).IsAssignableFrom(t));

        foreach (var seeder in seeders)
            services.AddScoped(typeof(IDevelopmentSeeder), seeder);

        return services;
    }

    /// <summary>
    /// Runs all registered seeders if <c>--seed</c> was passed. Refuses to run outside Development.
    /// Call after migrations and built-in startup seeding so seeders can rely on that data.
    /// </summary>
    public static async Task RunDevelopmentSeedersAsync(this WebApplication app, string[] args, CancellationToken cancellationToken = default)
    {
        if (!IsSeedRequested(args))
            return;

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentSeed");

        if (!app.Environment.IsDevelopment())
        {
            logger.LogWarning("{Argument} was passed but the environment is '{Environment}'; skipping development seeding",
                SeedArgument, app.Environment.EnvironmentName);
            return;
        }

        using var scope = app.Services.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<IDevelopmentSeeder>().OrderBy(s => s.Order).ToList();

        logger.LogInformation("Running {Count} development seeder(s)", seeders.Count);

        foreach (var seeder in seeders)
        {
            var name = seeder.GetType().Name;
            logger.LogInformation("Running development seeder '{Seeder}'", name);
            await seeder.SeedAsync(scope.ServiceProvider, cancellationToken);
        }

        logger.LogInformation("Development seeding complete");
    }
}