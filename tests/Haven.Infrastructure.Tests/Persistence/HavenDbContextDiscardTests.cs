using Haven.Domain.Aggregates;
using Haven.Domain.Entities;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;
using Haven.Infrastructure.Persistence;
using Haven.Testing.Common;

using Microsoft.EntityFrameworkCore;

using Shouldly;

namespace Haven.Infrastructure.Tests.Persistence;

[Category("Unit")]
public sealed class HavenDbContextDiscardTests
{
    private HavenDbContext _context = null!;

    [SetUp]
    public void Setup() => _context = TestDbContextFactory.CreateUnitDbContext();

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public async Task DiscardUnsavedChanges_ShouldNotPersistCollectionEditsOnTrackedAggregate()
    {
        var project = Project.Create("proj", "desc");
        var environment = project.AddEnvironment("dev", "desc");
        var service = environment.AddService("svc", ServiceType.DockerImage, ExposureMode.Internal,
            sourceConfig: new DockerConfig { Image = "img" });
        _context.Add(project);
        await _context.SaveChangesAsync();

        service.AddVolume(VolumeType.Named, "data", "/data", "data-vol");
        _context.ChangeTracker.DetectChanges();

        _context.DiscardUnsavedChanges();
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Entries().ShouldBeEmpty();
        (await _context.Set<ServiceVolume>().CountAsync()).ShouldBe(0);
    }
}
