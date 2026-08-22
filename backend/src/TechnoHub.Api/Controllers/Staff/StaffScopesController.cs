using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechnoHub.Api.Authorization;
using TechnoHub.Application.Scopes;
using TechnoHub.Application.Scopes.Dtos;

namespace TechnoHub.Api.Controllers.Staff;

/// <summary>The catalogue of assignable permissions.</summary>
[ApiController]
[Route("api/staff/scopes")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicyNames.StaffOnly)]
public sealed class StaffScopesController : ControllerBase
{
    private readonly IScopeCatalogService _scopeCatalog;

    public StaffScopesController(IScopeCatalogService scopeCatalog)
    {
        _scopeCatalog = scopeCatalog;
    }

    /// <summary>Lists every assignable scope, grouped by module.</summary>
    /// <remarks>
    /// Served from the same compile-time constants the authorization policies are built from, so the
    /// admin UI can render its checkboxes without hardcoding the list — and can never offer a scope
    /// the backend would not recognise.
    ///
    /// Requires only a valid staff token rather than a specific scope: the response is a static list
    /// of permission names with no account data in it, and later modules will want it for labels.
    /// </remarks>
    /// <response code="200">The scope catalogue, grouped by module in display order.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ScopeGroupResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ScopeGroupResponse>> GetGrouped() =>
        Ok(_scopeCatalog.GetGrouped());

    /// <summary>Lists every assignable scope as a flat array.</summary>
    /// <response code="200">The scope catalogue.</response>
    [HttpGet("flat")]
    [ProducesResponseType(typeof(IReadOnlyList<ScopeResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ScopeResponse>> GetAll() =>
        Ok(_scopeCatalog.GetAll());
}
