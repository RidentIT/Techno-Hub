using Microsoft.AspNetCore.Authorization;

namespace TechnoHub.Api.Authorization;

/// <summary>
/// Requires the caller to hold a specific scope. Satisfied by a matching <c>scope</c> claim, or
/// unconditionally by the Admin role.
/// </summary>
/// <param name="Scope">The required scope key, e.g. <c>inventory.manage</c>.</param>
public sealed record ScopeRequirement(string Scope) : IAuthorizationRequirement;

/// <summary>
/// Requires nothing more than a valid staff token. Used as the baseline on every staff controller
/// so that a route under <c>/api/staff</c> can never be reached by a token from another identity
/// space, even if someone forgets a more specific policy.
/// </summary>
public sealed record StaffTokenRequirement : IAuthorizationRequirement;

/// <summary>Requires the Admin role. Scopes are not consulted.</summary>
public sealed record AdminRoleRequirement : IAuthorizationRequirement;
