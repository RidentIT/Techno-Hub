namespace TechnoHub.Domain.Constants;

/// <summary>
/// The three fixed staff roles. There is no customer/client identity in this system —
/// every account represented by these roles is an internal Techno Hub staff member.
/// </summary>
public static class RoleNames
{
    /// <summary>Full system access. Bypasses every scope check.</summary>
    public const string Admin = "Admin";

    /// <summary>Handles repair/service jobs. Seeded with the repairs scopes, may be granted more.</summary>
    public const string Technician = "Technician";

    /// <summary>General staff. No scopes at all until an Admin assigns them.</summary>
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = new[] { Admin, Technician, User };

    /// <summary>Roles an Admin is allowed to create through the register endpoint.</summary>
    public static readonly IReadOnlyList<string> Assignable = new[] { Technician, User };

    public static bool IsValid(string? role) =>
        role is not null && All.Contains(role, StringComparer.Ordinal);

    public static bool IsAssignable(string? role) =>
        role is not null && Assignable.Contains(role, StringComparer.Ordinal);
}
