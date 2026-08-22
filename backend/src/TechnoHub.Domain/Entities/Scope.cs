namespace TechnoHub.Domain.Entities;

/// <summary>
/// A single permission string (e.g. <c>inventory.manage</c>). Rows are seeded from
/// <see cref="Constants.ScopeNames.All"/> and are never created at runtime — the table exists
/// so that UserScopes can be a proper foreign-keyed join rather than free text.
/// </summary>
public class Scope
{
    public int Id { get; set; }

    /// <summary>The permission string as it appears in the JWT, e.g. <c>inventory.manage</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Module label used to group scopes in the admin UI.</summary>
    public string Module { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ICollection<UserScope> UserScopes { get; set; } = new List<UserScope>();
}
