using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TechnoHub.Application.Common.Exceptions;
using TechnoHub.Application.Staff;
using TechnoHub.Application.Staff.Dtos;
using TechnoHub.Domain.Constants;
using TechnoHub.Domain.Entities;
using TechnoHub.Infrastructure.Auth;
using TechnoHub.Infrastructure.Persistence;

namespace TechnoHub.Infrastructure.Staff;

/// <inheritdoc />
/// <remarks>Internal on purpose: consumers depend on <see cref="IStaffUserService"/>.</remarks>
internal sealed class StaffUserService : IStaffUserService
{
    private readonly TechnoHubDbContext _db;
    private readonly StaffUserReader _reader;
    private readonly RefreshTokenRevoker _revoker;
    private readonly ILogger<StaffUserService> _logger;

    public StaffUserService(
        TechnoHubDbContext db,
        StaffUserReader reader,
        RefreshTokenRevoker revoker,
        ILogger<StaffUserService> logger)
    {
        _db = db;
        _reader = reader;
        _revoker = revoker;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StaffUserResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        // Active accounts first, then alphabetically — the order the staff screen wants.
        var users = await _db.Users
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        return await _reader.BuildManyAsync(users, cancellationToken);
    }

    public async Task<StaffUserResponse> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId, cancellationToken);
        return await _reader.BuildAsync(user, cancellationToken);
    }

    public async Task<StaffUserResponse> UpdateScopesAsync(
        Guid userId,
        UpdateUserScopesRequest request,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId, cancellationToken);
        var role = await _reader.GetRoleAsync(userId, cancellationToken);

        // Rejected rather than silently accepted: an Admin passes every scope check by role, so
        // storing scopes for one would create rows that imply a permission model that isn't real.
        if (string.Equals(role, RoleNames.Admin, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Admin accounts already have full access and cannot be assigned scopes. " +
                "Change the account's role first if you want scope-limited access.",
                "admin_scopes_not_assignable");
        }

        var requested = (request.Scopes ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unknown = requested.Where(key => !ScopeNames.IsValid(key)).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationFailedException(
                "scopes",
                $"Unknown scope(s): {string.Join(", ", unknown)}.");
        }

        var scopeIdByKey = await _db.Scopes
            .Where(s => requested.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Id, StringComparer.Ordinal, cancellationToken);

        if (scopeIdByKey.Count != requested.Count)
        {
            var missing = requested.Except(scopeIdByKey.Keys, StringComparer.Ordinal);
            throw new InvalidOperationException(
                $"Scope(s) {string.Join(", ", missing)} are defined in code but missing from the " +
                "Scopes table. Restart the API so the seeder can sync the catalogue.");
        }

        var existing = await _db.UserScopes
            .Where(us => us.UserId == userId)
            .ToListAsync(cancellationToken);

        var targetIds = scopeIdByKey.Values.ToHashSet();
        var existingIds = existing.Select(us => us.ScopeId).ToHashSet();

        // Diff rather than delete-all-then-reinsert, so GrantedAt/GrantedByUserId survive on the
        // grants that were already there.
        var toRemove = existing.Where(us => !targetIds.Contains(us.ScopeId)).ToList();
        var toAddIds = targetIds.Where(id => !existingIds.Contains(id)).ToList();

        if (toRemove.Count == 0 && toAddIds.Count == 0)
        {
            _logger.LogInformation(
                "Admin {AdminId} submitted an unchanged scope set for staff {UserId}; nothing to do.",
                actingUserId, userId);

            return StaffUserReader.Project(
                user,
                role,
                requested.OrderBy(k => k, StringComparer.Ordinal).ToList());
        }

        _db.UserScopes.RemoveRange(toRemove);

        foreach (var scopeId in toAddIds)
        {
            _db.UserScopes.Add(new UserScope
            {
                UserId = userId,
                ScopeId = scopeId,
                GrantedAt = DateTimeOffset.UtcNow,
                GrantedByUserId = actingUserId,
            });
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;

        // Scopes are baked into the access token, so the user keeps their old permissions until it
        // expires. Revoking the refresh tokens caps that window at one access-token lifetime and
        // forces a fresh login that picks up the new set.
        var revoked = await _revoker.RevokeActiveAsync(
            userId, RefreshTokenRevocationReasons.ScopesChanged, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} updated scopes for staff {UserId}: +{Added} -{Removed}, now {Scopes}. " +
            "{RevokedCount} refresh token(s) revoked.",
            actingUserId, userId, toAddIds.Count, toRemove.Count, requested, revoked);

        return StaffUserReader.Project(
            user,
            role,
            requested.OrderBy(k => k, StringComparer.Ordinal).ToList());
    }

    public async Task<StaffUserResponse> UpdateStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId, cancellationToken);
        var role = await _reader.GetRoleAsync(userId, cancellationToken);

        if (user.IsActive == request.IsActive)
        {
            _logger.LogInformation(
                "Staff {UserId} is already {Status}; no change made.",
                userId, request.IsActive ? "active" : "deactivated");

            return await _reader.BuildAsync(user, cancellationToken);
        }

        if (!request.IsActive)
        {
            // Locking yourself out is almost never intended, and is easy to do by accident on a
            // screen that lists your own account alongside everyone else's.
            if (userId == actingUserId)
            {
                throw new ConflictException(
                    "You cannot deactivate your own account.",
                    "cannot_deactivate_self");
            }

            // There is no public registration and no password-reset flow yet, so losing the last
            // Admin means losing the only way back into the system.
            if (string.Equals(role, RoleNames.Admin, StringComparison.Ordinal)
                && await CountOtherActiveAdminsAsync(userId, cancellationToken) == 0)
            {
                throw new ConflictException(
                    "This is the last active Admin account and cannot be deactivated. " +
                    "Create another Admin first.",
                    "last_active_admin");
            }
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var revoked = 0;
        if (!request.IsActive)
        {
            // Kills their sessions now rather than at the next refresh.
            revoked = await _revoker.RevokeActiveAsync(
                userId, RefreshTokenRevocationReasons.AccountDeactivated, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} set staff {UserId} to {Status}. Reason: {Reason}. " +
            "{RevokedCount} refresh token(s) revoked.",
            actingUserId, userId, request.IsActive ? "active" : "deactivated",
            request.Reason ?? "(none given)", revoked);

        return await _reader.BuildAsync(user, cancellationToken);
    }

    private async Task<ApplicationUser> FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw NotFoundException.For("Staff account", userId);

    /// <summary>Active Admin accounts other than <paramref name="excludingUserId"/>.</summary>
    private async Task<int> CountOtherActiveAdminsAsync(Guid excludingUserId, CancellationToken cancellationToken) =>
        await (
            from userRole in _db.UserRoles
            join r in _db.Roles on userRole.RoleId equals r.Id
            join u in _db.Users on userRole.UserId equals u.Id
            where r.Name == RoleNames.Admin && u.IsActive && u.Id != excludingUserId
            select u.Id)
            .CountAsync(cancellationToken);
}
