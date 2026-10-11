using Haven.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Haven.Infrastructure.Persistence.Configuration;

public class ApiTokenScopeConfiguration : IEntityTypeConfiguration<ApiTokenScope>
{
    public void Configure(EntityTypeBuilder<ApiTokenScope> builder)
    {
        builder.ToTable("api_token_scopes");
        
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        
        builder.Property(s => s.ApiTokenId)
            .HasColumnName("api_token_id")
            .IsRequired();
        
        builder.Property(s => s.Scope)
            .HasColumnName("scope")
            .IsRequired()
            .HasMaxLength(255);
    }
}