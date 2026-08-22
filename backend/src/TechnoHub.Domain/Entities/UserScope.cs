namespace TechnoHub.Domain.Entities;

/// <summary>
/// Join row granting one <see cref="Scope"/> to one <see cref="ApplicationUser"/>.
/// Completely independent of the user's role: this table is the only source of truth for
/// what a non-Admin account may do.
/// </summary>
public class UserScope
{
    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public int ScopeId { get; set; }

    public Scope? Scope { get; set; }

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The Admin who granted the scope. Null when granted by the system seeder.</summary>
    public Guid? GrantedByUserId { get; set; }
}
