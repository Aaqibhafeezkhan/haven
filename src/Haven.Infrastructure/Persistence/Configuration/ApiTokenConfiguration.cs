using Haven.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Haven.Infrastructure.Persistence.Configuration;

public class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable("api_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(t => t.DisplayName)
            .HasColumnName("display_name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.Prefix)
            .HasColumnName("prefix")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(t => t.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(t => t.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.HasMany(t => t.Scopes)
            .WithOne(s => s.ApiToken)
            .HasForeignKey(s => s.ApiTokenId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}