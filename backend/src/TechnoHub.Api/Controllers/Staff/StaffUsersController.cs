using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechnoHub.Api.Authorization;
using TechnoHub.Api.Extensions;
using TechnoHub.Application.Staff;
using TechnoHub.Application.Staff.Dtos;
using TechnoHub.Domain.Constants;

namespace TechnoHub.Api.Controllers.Staff;

/// <summary>Admin management of staff accounts: who exists, what they can do, and whether they're active.</summary>
[ApiController]
[Route("api/staff/users")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicyNames.StaffOnly)]
public sealed class StaffUsersController : ControllerBase
{
    private readonly IStaffUserService _staffUserService;

    public StaffUsersController(IStaffUserService staffUserService)
    {
        _staffUserService = staffUserService;
    }

    /// <summary>Lists every staff account, active ones first.</summary>
    /// <remarks>
    /// Scope-gated rather than Admin-only so that a manager can be granted read access to the staff
    /// list without also being able to create accounts or change permissions.
    /// </remarks>
    /// <response code="200">The staff list.</response>
    [Authorize(Policy = ScopeNames.StaffView)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StaffUserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StaffUserResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _staffUserService.GetAllAsync(cancellationToken));

    /// <summary>Returns one staff account.</summary>
    /// <param name="id">The staff account id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The staff account.</response>
    /// <response code="404">No account with that id.</response>
    [Authorize(Policy = ScopeNames.StaffView)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StaffUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaffUserResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _staffUserService.GetByIdAsync(id, cancellationToken));

    /// <summary>Replaces a staff account's assigned scopes.</summary>
    /// <remarks>
    /// The list is absolute, not a delta: whatever you send becomes the account's complete scope set,
    /// and an empty array strips every permission. Grants that survive keep their original
    /// <c>grantedAt</c>.
    ///
    /// Because scopes are baked into the access token, the change lands on the user's next token.
    /// All of their refresh tokens are revoked here, so the stale token cannot outlive its own
    /// expiry — worst case they keep the old permissions for the remainder of one access-token
    /// lifetime, then have to sign in again.
    ///
    /// Admin accounts are rejected: the Admin role already bypasses every scope check.
    /// </remarks>
    /// <param name="id">The staff account id.</param>
    /// <param name="request">The complete set of scope keys the account should hold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The updated account.</response>
    /// <response code="400">An unknown or duplicated scope key was supplied.</response>
    /// <response code="404">No account with that id.</response>
    /// <response code="409">The target account is an Admin.</response>
    [Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
    [HttpPatch("{id:guid}/scopes")]
    [ProducesResponseType(typeof(StaffUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffUserResponse>> UpdateScopes(
        Guid id,
        [FromBody] UpdateUserScopesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _staffUserService.UpdateScopesAsync(
            id, request, User.GetStaffUserId(), cancellationToken));

    /// <summary>Activates or deactivates a staff account.</summary>
    /// <remarks>
    /// Staff are never hard-deleted, so records that reference them stay intact. Deactivating blocks
    /// login, revokes every refresh token immediately, and is refused for your own account or for the
    /// last remaining active Admin.
    /// </remarks>
    /// <param name="id">The staff account id.</param>
    /// <param name="request">The desired status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The updated account.</response>
    /// <response code="404">No account with that id.</response>
    /// <response code="409">You tried to deactivate yourself or the last active Admin.</response>
    [Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(StaffUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffUserResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _staffUserService.UpdateStatusAsync(
            id, request, User.GetStaffUserId(), cancellationToken));
}
