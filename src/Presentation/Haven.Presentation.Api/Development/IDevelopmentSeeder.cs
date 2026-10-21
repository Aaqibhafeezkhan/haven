namespace Haven.Presentation.Api.Development;

/// <summary>
/// A unit of development data seeding. Implementations are discovered automatically from this
/// assembly and run (in <see cref="Order"/>) when the app is started with <c>--seed</c> in the
/// Development environment. Seeders must be idempotent: they run on every <c>--seed</c> start, so
/// check for existing data before adding.
/// </summary>
public interface IDevelopmentSeeder
{
    /// <summary>Lower values run first. Use this to seed dependencies (e.g. projects before services).</summary>
    int Order => 0;

    Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken);
}