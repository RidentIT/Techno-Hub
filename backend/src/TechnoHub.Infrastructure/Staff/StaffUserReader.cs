using Microsoft.EntityFrameworkCore;
using TechnoHub.Application.Staff.Dtos;
using TechnoHub.Domain.Constants;
using TechnoHub.Domain.Entities;
using TechnoHub.Infrastructure.Persistence;

namespace TechnoHub.Infrastructure.Staff;

/// <summary>
/// Loads the role and scope keys that belong to a staff account and projects the result into
/// <see cref="StaffUserResponse"/>. Shared by the auth and user-management services so the shape of
/// a "staff user" is defined in exactly one place.
/// </summary>
internal sealed class StaffUserReader
{
    private readonly TechnoHubDbContext _db;

    public StaffUserReader(TechnoHubDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// The account's role. A staff member holds exactly one; if the row is somehow missing we fall
    /// back to <see cref="RoleNames.User"/>, the role with no permissions, rather than guessing up.
    /// </summary>
    public async Task<string> GetRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var role = await (
            from userRole in _db.UserRoles
            join r in _db.Roles on userRole.RoleId equals r.Id
            where userRole.UserId == userId
            select r.Name).FirstOrDefaultAsync(cancellationToken);

        return role ?? RoleNames.User;
    }

    /// <summary>Scope keys explicitly granted to the account, sorted for stable output.</summary>
    public async Task<List<string>> GetScopeKeysAsync(Guid userId, CancellationToken cancellationToken) =>
        await _db.UserScopes
            .Where(us => us.UserId == userId)
            .Select(us => us.Scope!.Key)
            .OrderBy(key => key)
            .ToListAsync(cancellationToken);

    public async Task<StaffUserResponse> BuildAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var role = await GetRoleAsync(user.Id, cancellationToken);
        var scopes = await GetScopeKeysAsync(user.Id, cancellationToken);

        return Project(user, role, scopes);
    }

    /// <summary>
    /// Batched projection for list endpoints: two extra queries in total rather than two per user.
    /// </summary>
    public async Task<IReadOnlyList<StaffUserResponse>> BuildManyAsync(
        IReadOnlyList<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return Array.Empty<StaffUserResponse>();
        }

        var userIds = users.Select(u => u.Id).ToList();

        var roleRows = await (
            from userRole in _db.UserRoles
            join r in _db.Roles on userRole.RoleId equals r.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, RoleName = r.Name })
            .ToListAsync(cancellationToken);

        // Grouped rather than keyed directly: a staff member should hold exactly one role, but a
        // hand-edited StaffUserRoles row would otherwise make this list endpoint throw on a
        // duplicate key. Keep the first and carry on.
        var roleByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().RoleName);

        var scopeRows = await _db.UserScopes
            .Where(us => userIds.Contains(us.UserId))
            .Select(us => new { us.UserId, Key = us.Scope!.Key })
            .ToListAsync(cancellationToken);

        var scopesByUser = scopeRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.Key).OrderBy(k => k, StringComparer.Ordinal).ToList());

        return users
            .Select(user => Project(
                user,
                roleByUser.GetValueOrDefault(user.Id) ?? RoleNames.User,
                scopesByUser.GetValueOrDefault(user.Id) ?? Array.Empty<string>()))
            .ToList();
    }

    /// <summary>
    /// Maps to the response DTO. <c>HasAllScopes</c> is derived from the role rather than from the
    /// scope list, because an Admin passes every check while holding no scope rows at all.
    /// </summary>
    public static StaffUserResponse Project(
        ApplicationUser user,
        string role,
        IReadOnlyList<string> scopes) =>
        new(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            UserName: user.UserName ?? string.Empty,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            Role: role,
            IdentityType: IdentityTypes.Staff,
            IsActive: user.IsActive,
            Scopes: scopes,
            HasAllScopes: string.Equals(role, RoleNames.Admin, StringComparison.Ordinal),
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt);
}
