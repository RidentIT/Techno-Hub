using TechnoHub.Application.Scopes.Dtos;
using TechnoHub.Domain.Constants;

namespace TechnoHub.Application.Scopes;

/// <summary>
/// Projects the compile-time scope catalogue into DTOs. Reads from
/// <see cref="ScopeNames.All"/> rather than the database so the API can never advertise a scope
/// the authorization policies don't know about.
/// </summary>
public sealed class ScopeCatalogService : IScopeCatalogService
{
    private readonly IReadOnlyList<ScopeResponse> _all;
    private readonly IReadOnlyList<ScopeGroupResponse> _grouped;

    public ScopeCatalogService()
    {
        _all = ScopeNames.All
            .Select(s => new ScopeResponse(s.Key, s.Module, s.Description))
            .ToList();

        // GroupBy preserves first-seen order, which is the display order in ScopeNames.All.
        _grouped = _all
            .GroupBy(s => s.Module, StringComparer.Ordinal)
            .Select(g => new ScopeGroupResponse(g.Key, g.ToList()))
            .ToList();
    }

    public IReadOnlyList<ScopeResponse> GetAll() => _all;

    public IReadOnlyList<ScopeGroupResponse> GetGrouped() => _grouped;
}
