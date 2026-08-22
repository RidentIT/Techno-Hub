using TechnoHub.Application.Scopes.Dtos;

namespace TechnoHub.Application.Scopes;

/// <summary>
/// Exposes the fixed scope catalogue so the admin UI can render assignment checkboxes without
/// hardcoding the list on the frontend.
/// </summary>
public interface IScopeCatalogService
{
    /// <summary>Every scope, grouped by module, in display order.</summary>
    IReadOnlyList<ScopeGroupResponse> GetGrouped();

    /// <summary>Every scope, flat.</summary>
    IReadOnlyList<ScopeResponse> GetAll();
}
