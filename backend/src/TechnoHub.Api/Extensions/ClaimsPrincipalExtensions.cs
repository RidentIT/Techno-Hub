using System.Security.Claims;
using TechnoHub.Domain.Constants;

namespace TechnoHub.Api.Extensions;

/// <summary>Reads the Techno Hub claims off an authenticated principal.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The staff account id from the <c>sub</c> claim.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The claim is missing or malformed. This should be impossible on an endpoint behind
    /// <c>[Authorize]</c>, so it is a bug rather than a client error.
    /// </exception>
    public static Guid GetStaffUserId(this ClaimsPrincipal principal)
    {
        // MapInboundClaims is off on the bearer handler, so "sub" arrives unmapped. The
        // NameIdentifier fallback keeps this working if that ever changes.
        var raw = principal.FindFirstValue("sub")
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(raw, out var userId))
        {
            throw new InvalidOperationException(
                "The authenticated principal has no usable 'sub' claim. Check the JWT bearer " +
                "configuration — this endpoint requires a token issued by this API.");
        }

        return userId;
    }

    /// <summary>The role claim, or null.</summary>
    public static string? GetRole(this ClaimsPrincipal principal) =>
        principal.FindFirstValue("role");

    /// <summary>Every scope claim on the token.</summary>
    public static IReadOnlyList<string> GetScopes(this ClaimsPrincipal principal) =>
        principal.FindAll(ClaimTypesExtended.Scope).Select(claim => claim.Value).ToList();

    /// <summary>True when the token belongs to the staff identity space.</summary>
    public static bool IsStaffToken(this ClaimsPrincipal principal) =>
        string.Equals(
            principal.FindFirstValue(ClaimTypesExtended.TokenType),
            IdentityTypes.Staff,
            StringComparison.Ordinal);
}
