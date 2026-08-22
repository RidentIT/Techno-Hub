using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TechnoHub.Domain.Entities;

namespace TechnoHub.Infrastructure.Persistence;

/// <summary>
/// The single application DbContext. Extends the Identity context so ASP.NET Core Identity owns
/// the user store and password hashing, and adds the scope and refresh-token tables on top.
/// </summary>
public class TechnoHubDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public TechnoHubDbContext(DbContextOptions<TechnoHubDbContext> options) : base(options)
    {
    }

    public DbSet<Scope> Scopes => Set<Scope>();

    public DbSet<UserScope> UserScopes => Set<UserScope>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(TechnoHubDbContext).Assembly);

        // Identity's own join tables. Prefixed "Staff" for the same reason the JWT carries
        // type=staff: if a second identity space is ever added, it gets its own tables and there
        // is no ambiguity about which "Users" table means what.
        builder.Entity<IdentityUserRole<Guid>>().ToTable("StaffUserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("StaffUserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("StaffUserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("StaffUserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("StaffRoleClaims");
    }
}
