using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechnoHub.Domain.Entities;

namespace TechnoHub.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        // Every refresh is a lookup by hash, and two tokens must never collide.
        builder.HasIndex(rt => rt.TokenHash).IsUnique();

        builder.Property(rt => rt.CreatedAt).IsRequired();
        builder.Property(rt => rt.ExpiresAt).IsRequired();

        builder.Property(rt => rt.RevokedReason).HasMaxLength(64);
        builder.Property(rt => rt.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(rt => rt.CreatedByIp).HasMaxLength(64);

        // Supports "revoke everything still live for this user" on logout, scope change and
        // deactivation.
        builder.HasIndex(rt => new { rt.UserId, rt.RevokedAt });
    }
}
