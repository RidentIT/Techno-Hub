namespace TechnoHub.Application.Scopes.Dtos;

/// <summary>A single assignable permission.</summary>
/// <param name="Key">The scope string, e.g. <c>inventory.manage</c>.</param>
/// <param name="Module">Module label used to group the scope in the UI.</param>
/// <param name="Description">Human-readable explanation shown next to the checkbox.</param>
public sealed record ScopeResponse(string Key, string Module, string Description);

/// <summary>Scopes for one module, so the admin UI can render grouped checkboxes.</summary>
/// <param name="Module">Module label.</param>
/// <param name="Scopes">The scopes belonging to that module.</param>
public sealed record ScopeGroupResponse(string Module, IReadOnlyList<ScopeResponse> Scopes);
