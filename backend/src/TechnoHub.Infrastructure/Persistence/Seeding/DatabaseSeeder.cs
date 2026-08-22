using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechnoHub.Application.Common.Options;
using TechnoHub.Domain.Constants;
using TechnoHub.Domain.Entities;

namespace TechnoHub.Infrastructure.Persistence.Seeding;

/// <summary>
/// Brings the database up to a usable state on startup: the three roles, the scope catalogue and
/// the single root Admin. Every step is idempotent, so it is safe to run on every boot.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly TechnoHubDbContext _db;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SeedAdminOptions _seedAdmin;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        TechnoHubDbContext db,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<SeedAdminOptions> seedAdmin,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _roleManager = roleManager;
        _userManager = userManager;
        _seedAdmin = seedAdmin.Value;
        _logger = logger;
    }

    /// <summary>Applies any pending EF Core migrations.</summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var pending = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            _logger.LogInformation("Database schema is up to date; no migrations to apply.");
            return;
        }

        _logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Count, pending);
        await _db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Migrations applied.");
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedScopesAsync(cancellationToken);
        await SeedRootAdminAsync();
    }

    private async Task SeedRolesAsync()
    {
        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RoleNames.Admin] = "Full system access. Bypasses all scope checks.",
            [RoleNames.Technician] = "Handles repair and service jobs. Granted the repairs scopes on creation.",
            [RoleNames.User] = "General staff. Holds no permissions until an Admin assigns scopes.",
        };

        foreach (var roleName in RoleNames.All)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new ApplicationRole(roleName)
            {
                Description = descriptions[roleName],
            });

            if (result.Succeeded)
            {
                _logger.LogInformation("Seeded role {Role}.", roleName);
            }
            else
            {
                // A role that cannot be created leaves the system unusable, so fail loudly.
                throw new InvalidOperationException(
                    $"Failed to seed role '{roleName}': {Describe(result)}");
            }
        }
    }

    private async Task SeedScopesAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Scopes.ToDictionaryAsync(s => s.Key, StringComparer.Ordinal, cancellationToken);

        var added = 0;
        var updated = 0;

        foreach (var definition in ScopeNames.All)
        {
            if (!existing.TryGetValue(definition.Key, out var row))
            {
                _db.Scopes.Add(new Scope
                {
                    Key = definition.Key,
                    Module = definition.Module,
                    Description = definition.Description,
                });
                added++;
                continue;
            }

            // Keep wording in sync with the code without touching the row's identity, so existing
            // grants in UserScopes survive a description change.
            if (row.Module != definition.Module || row.Description != definition.Description)
            {
                row.Module = definition.Module;
                row.Description = definition.Description;
                updated++;
            }
        }

        // Scopes removed from the catalogue are reported, not deleted: deleting would cascade away
        // the grants in UserScopes, which should be a deliberate migration rather than a side
        // effect of editing a constant.
        var orphaned = existing.Keys.Where(key => !ScopeNames.AllKeys.Contains(key)).ToList();
        if (orphaned.Count > 0)
        {
            _logger.LogWarning(
                "The Scopes table contains {Count} scope(s) no longer defined in ScopeNames: {Scopes}. " +
                "They were left in place; remove them with a migration if they are truly gone.",
                orphaned.Count, orphaned);
        }

        if (added > 0 || updated > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Scope catalogue synced: {Added} added, {Updated} updated.", added, updated);
        }
    }

    private async Task SeedRootAdminAsync()
    {
        if (!_seedAdmin.IsConfigured)
        {
            _logger.LogWarning(
                "SeedAdmin:Email / SeedAdmin:Password are not configured, so no Admin account was " +
                "created. Set SeedAdmin__Email and SeedAdmin__Password and restart — there is no " +
                "public registration endpoint, so an Admin is the only way into the system.");
            return;
        }

        var email = _seedAdmin.Email!.Trim();
        var existing = await _userManager.FindByEmailAsync(email);

        if (existing is not null)
        {
            // Never rewrite the password of an existing account from configuration — that would
            // silently reset a live Admin every time the app restarts.
            if (!await _userManager.IsInRoleAsync(existing, RoleNames.Admin))
            {
                await _userManager.AddToRoleAsync(existing, RoleNames.Admin);
                _logger.LogInformation("Existing account {Email} was added to the Admin role.", email);
            }

            _logger.LogInformation("Admin account {Email} already exists; seeding skipped.", email);
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = string.IsNullOrWhiteSpace(_seedAdmin.UserName) ? email : _seedAdmin.UserName!.Trim(),
            FullName = string.IsNullOrWhiteSpace(_seedAdmin.FullName) ? "System Administrator" : _seedAdmin.FullName!.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var created = await _userManager.CreateAsync(admin, _seedAdmin.Password!);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create the seed Admin account: {Describe(created)}");
        }

        var roleAssigned = await _userManager.AddToRoleAsync(admin, RoleNames.Admin);
        if (!roleAssigned.Succeeded)
        {
            throw new InvalidOperationException(
                $"Created the seed Admin but could not assign the Admin role: {Describe(roleAssigned)}");
        }

        // Deliberately no UserScopes rows: the Admin role bypasses scope checks entirely.
        _logger.LogInformation("Seeded root Admin account {Email}.", email);
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
