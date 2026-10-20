using Haven.Domain.Aggregates;
using Haven.Domain.Entities;
using Haven.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Haven.Infrastructure.Persistence.Configuration;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("services");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.EnvironmentId)
            .HasColumnName("environment_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired();

        builder.Property(x => x.Alias)
            .HasColumnName("alias")
            .HasMaxLength(8);

        builder.HasIndex(nameof(Service.EnvironmentId), nameof(Service.Alias))
            .IsUnique()
            .HasFilter("\"alias\" IS NOT NULL");

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(x => x.ExposureMode)
            .HasColumnName("exposure_mode")
            .IsRequired();

        builder.Property(x => x.DesiredState)
            .HasColumnName("desired_state")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(x => x.Token)
            .HasColumnName("token")
            .IsRequired();

        builder.Property(x => x.SourceConfigJson)
            .HasColumnName("source_config")
            .HasColumnType("TEXT");

        // Stored as JSON: repository URLs and file paths can contain any delimiter a flat string format would use.
        builder.Property(x => x.RepositoryBinding)
            .HasColumnName("repository_binding")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<RepositoryBinding>(v, (System.Text.Json.JsonSerializerOptions?)null),
                new ValueComparer<RepositoryBinding?>(
                    (a, b) => a == b,
                    v => v == null ? 0 : v.GetHashCode(),
                    v => v == null
                        ? null
                        : new RepositoryBinding
                        {
                            GitCredentialId = v.GitCredentialId,
                            Repository = v.Repository,
                            Branch = v.Branch,
                            FilePath = v.FilePath
                        }))
            .HasColumnType("TEXT");

        builder.Ignore(x => x.SourceConfig);

        builder.HasMany(x => x.ServiceNetworks)
            .WithOne()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.ServiceNetworks)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}