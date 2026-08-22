using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TechnoHub.Domain.Constants;

namespace TechnoHub.Api.Authorization;

/// <summary>
/// Decides scope requirements purely from claims on the incoming JWT — no database round trip on
/// the request path.
/// </summary>
/// <remarks>
/// The order of the checks is the authorization model in miniature:
/// <list type="number">
/// <item>the token must belong to the staff identity space (<c>type=staff</c>);</item>
/// <item>an Admin passes everything, regardless of what scopes it holds;</item>
/// <item>everyone else needs a literal matching <c>scope</c> claim.</item>
/// </list>
/// Nothing here is role-specific beyond Admin: a Technician and a User with the same scopes have
/// exactly the same access.
/// </remarks>
public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    private readonly ILogger<ScopeAuthorizationHandler> _logger;

    public ScopeAuthorizationHandler(ILogger<ScopeAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (!IsStaffToken(user))
        {
            _logger.LogWarning(
                "Denied scope {Scope}: token identity type is {TokenType}, expected {Expected}.",
                requirement.Scope,
                user.FindFirstValue(ClaimTypesExtended.TokenType) ?? "(absent)",
                IdentityTypes.Staff);

            return Task.CompletedTask;
        }

        if (user.IsInRole(RoleNames.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var hasScope = user
            .FindAll(ClaimTypesExtended.Scope)
            .Any(claim => string.Equals(claim.Value, requirement.Scope, StringComparison.Ordinal));

        if (hasScope)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Denied scope {Scope} for staff {UserId}: token carries {GrantedScopes}.",
            requirement.Scope,
            user.FindFirstValue("sub") ?? "(unknown)",
            user.FindAll(ClaimTypesExtended.Scope).Select(c => c.Value).ToArray());

        return Task.CompletedTask;
    }

    internal static bool IsStaffToken(ClaimsPrincipal user) =>
        string.Equals(
            user.FindFirstValue(ClaimTypesExtended.TokenType),
            IdentityTypes.Staff,
            StringComparison.Ordinal);
}

/// <summary>Passes any authenticated principal whose token belongs to the staff identity space.</summary>
public sealed class StaffTokenAuthorizationHandler : AuthorizationHandler<StaffTokenRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        StaffTokenRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && ScopeAuthorizationHandler.IsStaffToken(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Passes only the Admin role. Deliberately separate from <see cref="ScopeRequirement"/>: endpoints
/// that manage staff accounts are Admin-only by design, not scope-gated, so no combination of
/// granted scopes can reach them.
/// </summary>
public sealed class AdminRoleAuthorizationHandler : AuthorizationHandler<AdminRoleRequirement>
{
    private readonly ILogger<AdminRoleAuthorizationHandler> _logger;

    public AdminRoleAuthorizationHandler(ILogger<AdminRoleAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRoleRequirement requirement)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated != true || !ScopeAuthorizationHandler.IsStaffToken(user))
        {
            return Task.CompletedTask;
        }

        if (user.IsInRole(RoleNames.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Denied Admin-only endpoint for staff {UserId} with role {Role}.",
            user.FindFirstValue("sub") ?? "(unknown)",
            user.FindFirstValue("role") ?? "(none)");

        return Task.CompletedTask;
    }
}
