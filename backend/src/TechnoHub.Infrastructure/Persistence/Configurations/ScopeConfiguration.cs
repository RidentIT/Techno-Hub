using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechnoHub.Domain.Entities;

namespace TechnoHub.Infrastructure.Persistence.Configurations;

public sealed class ScopeConfiguration : IEntityTypeConfiguration<Scope>
{
    public void Configure(EntityTypeBuilder<Scope> builder)
    {
        builder.ToTable("Scopes");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key)
            .HasMaxLength(64)
            .IsRequired();

        // Scopes are looked up by key everywhere, and the key is the value that ends up in the JWT.
        builder.HasIndex(s => s.Key).IsUnique();

        builder.Property(s => s.Module)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(256)
            .IsRequired();
    }
}

public sealed class UserScopeConfiguration : IEntityTypeConfiguration<UserScope>
{
    public void Configure(EntityTypeBuilder<UserScope> builder)
    {
        builder.ToTable("UserScopes");

        // Composite key makes a duplicate grant impossible at the database level.
        builder.HasKey(us => new { us.UserId, us.ScopeId });

        builder.Property(us => us.GrantedAt)
            .IsRequired();

        builder.HasOne(us => us.Scope!)
            .WithMany(s => s.UserScopes)
            .HasForeignKey(us => us.ScopeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(us => us.ScopeId);
    }
}
