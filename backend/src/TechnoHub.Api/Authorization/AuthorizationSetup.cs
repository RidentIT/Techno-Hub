using Microsoft.AspNetCore.Authorization;
using TechnoHub.Domain.Constants;

namespace TechnoHub.Api.Authorization;

/// <summary>Well-known policy names that are not scope keys.</summary>
public static class AuthorizationPolicyNames
{
    /// <summary>Any valid staff token. The baseline on every <c>/api/staff</c> controller.</summary>
    public const string StaffOnly = "staff-only";

    /// <summary>Admin role required.</summary>
    public const string AdminOnly = "admin-only";
}

/// <summary>Wires up the scope-based authorization policies.</summary>
public static class AuthorizationSetup
{
    /// <summary>
    /// Registers one policy per scope, named after the scope key itself, so a controller can write
    /// <c>[Authorize(Policy = ScopeNames.InventoryManage)]</c> and get the scope check for free.
    /// Driven off <see cref="ScopeNames.All"/>, so adding a scope to the catalogue is all it takes
    /// to make it usable in an attribute — there is no second list to keep in sync.
    /// </summary>
    public static IServiceCollection AddStaffAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, StaffTokenAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, AdminRoleAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            // RequireAuthenticatedUser on every policy so an anonymous caller gets a 401 challenge
            // rather than a bare 403 — the frontend distinguishes "log in again" from "not allowed".
            options.AddPolicy(AuthorizationPolicyNames.StaffOnly, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new StaffTokenRequirement()));

            options.AddPolicy(AuthorizationPolicyNames.AdminOnly, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new AdminRoleRequirement()));

            foreach (var scope in ScopeNames.All)
            {
                options.AddPolicy(scope.Key, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new ScopeRequirement(scope.Key)));
            }

            // Applies to any endpoint that carries [Authorize] without naming a policy.
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new StaffTokenRequirement())
                .Build();
        });

        return services;
    }
}
