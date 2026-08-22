using Microsoft.AspNetCore.Identity;

namespace TechnoHub.Domain.Entities;

/// <summary>
/// An internal Techno Hub staff account. This is the only identity type in the system —
/// the public catalogue and quotation builder are fully anonymous, so there is deliberately
/// no customer entity here.
/// </summary>
/// <remarks>
/// Inherits <see cref="IdentityUser{TKey}"/>, which already supplies Email, UserName,
/// PhoneNumber, PasswordHash, SecurityStamp and the lockout columns.
/// </remarks>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Staff are soft-disabled, never hard-deleted, so historical records keep pointing at a
    /// real account. A deactivated user cannot log in and cannot refresh an existing token.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>The Admin who created this account. Null for the seeded root Admin.</summary>
    public Guid? CreatedByUserId { get; set; }

    public ICollection<UserScope> UserScopes { get; set; } = new List<UserScope>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
