using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TechnoHub.Api.Authorization;
using TechnoHub.Api.Extensions;
using TechnoHub.Application.Auth;
using TechnoHub.Application.Auth.Dtos;
using TechnoHub.Application.Common.Options;
using TechnoHub.Application.Staff.Dtos;

namespace TechnoHub.Api.Controllers.Staff;

/// <summary>
/// Staff authentication. Every route in the system lives under <c>/api/staff</c> so that the
/// anonymous public catalogue and quotation endpoints can later sit under <c>/api/public</c> and be
/// separated at the route level, not only by claims.
/// </summary>
[ApiController]
[Route("api/staff/auth")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicyNames.StaffOnly)]
public sealed class StaffAuthController : ControllerBase
{
    private readonly IStaffAuthService _authService;
    private readonly RefreshCookieOptions _cookieOptions;

    public StaffAuthController(
        IStaffAuthService authService,
        IOptions<RefreshCookieOptions> cookieOptions)
    {
        _authService = authService;
        _cookieOptions = cookieOptions.Value;
    }

    /// <summary>Signs a staff member in.</summary>
    /// <remarks>
    /// Accepts either the account's email or its username. On success the refresh token is both set
    /// as an httpOnly cookie and returned in the body — a browser client should rely on the cookie
    /// and never store the value itself.
    /// </remarks>
    /// <response code="200">Authenticated. Returns the access token plus the user's role and scopes.</response>
    /// <response code="400">The request body is missing required fields.</response>
    /// <response code="401">Wrong credentials, or the account is deactivated or locked out.</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, ClientIpAddress(), cancellationToken);

        Response.SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt, _cookieOptions);

        return Ok(result);
    }

    /// <summary>Exchanges a refresh token for a new access token.</summary>
    /// <remarks>
    /// The token is read from the httpOnly cookie, or from the request body when there is no cookie
    /// (which is how Swagger and server-side callers use this endpoint). Refresh tokens rotate: the
    /// one presented here is revoked and replaced. Presenting an already-used token is treated as a
    /// leak and revokes every live session for that account.
    ///
    /// This is also where a scope change reaches a signed-in user — role and scopes are re-read from
    /// the database and baked into the new access token.
    /// </remarks>
    /// <response code="200">A new token pair.</response>
    /// <response code="401">The refresh token is missing, unknown, expired, already used or revoked.</response>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        // Cookie wins over the body: a browser caller cannot read its own cookie, so a body value
        // present alongside a cookie is the less trustworthy of the two.
        var refreshToken = Request.ReadRefreshCookie(_cookieOptions) ?? request?.RefreshToken;

        var result = await _authService.RefreshAsync(refreshToken ?? string.Empty, ClientIpAddress(), cancellationToken);

        Response.SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt, _cookieOptions);

        return Ok(result);
    }

    /// <summary>Signs the caller out by revoking their refresh token.</summary>
    /// <remarks>
    /// Idempotent — an unknown or already-revoked token still returns 204. The access token itself
    /// stays valid until it expires, which is why its lifetime is short; the client should discard it.
    /// </remarks>
    /// <response code="204">The refresh token is revoked and the cookie cleared.</response>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var refreshToken = Request.ReadRefreshCookie(_cookieOptions) ?? request?.RefreshToken;

        await _authService.LogoutAsync(refreshToken, cancellationToken);

        Response.ClearRefreshCookie(_cookieOptions);

        return NoContent();
    }

    /// <summary>Creates a Technician or User account.</summary>
    /// <remarks>
    /// Admin-only, and the only way a staff account is created besides the seeded root Admin — there
    /// is no public self-registration endpoint in this system. Creating another Admin through here is
    /// rejected.
    ///
    /// Omit <c>scopes</c> to accept the role's default: a Technician is granted
    /// <c>repairs.view</c> and <c>repairs.manage</c> as ordinary revocable grants, and a User is
    /// granted nothing. Send an explicit list to override, including an empty one.
    /// </remarks>
    /// <response code="201">The account was created.</response>
    /// <response code="400">Validation failed, the role is not assignable, or a scope key is unknown.</response>
    /// <response code="409">The email or username is already taken.</response>
    [Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
    [HttpPost("register")]
    [ProducesResponseType(typeof(StaffUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffUserResponse>> Register(
        [FromBody] RegisterStaffRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _authService.RegisterStaffAsync(
            request, User.GetStaffUserId(), cancellationToken);

        return CreatedAtAction(
            actionName: nameof(StaffUsersController.GetById),
            controllerName: "StaffUsers",
            routeValues: new { id = created.Id },
            value: created);
    }

    /// <summary>Returns the authenticated staff member's profile, role and scopes.</summary>
    /// <remarks>
    /// Read fresh from the database rather than echoed from the token, so the frontend sees a scope
    /// change as soon as it reloads — even before the access token is refreshed.
    /// </remarks>
    /// <response code="200">The current user.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(StaffUserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffUserResponse>> Me(CancellationToken cancellationToken) =>
        Ok(await _authService.GetProfileAsync(User.GetStaffUserId(), cancellationToken));

    private string? ClientIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();
}
