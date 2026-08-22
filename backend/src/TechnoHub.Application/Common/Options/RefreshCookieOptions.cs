namespace TechnoHub.Application.Common.Options;

/// <summary>
/// Shape of the httpOnly refresh-token cookie the API sets, bound from <c>RefreshCookie</c>.
/// </summary>
/// <remarks>
/// In the local setup the admin app talks to the API through its own Next.js route handlers
/// (a BFF), so the browser-facing cookie is same-site on the Next origin. The API still sets its
/// own cookie so that Swagger and any direct API consumer get the same refresh behaviour.
/// </remarks>
public sealed class RefreshCookieOptions
{
    public const string SectionName = "RefreshCookie";

    public string Name { get; set; } = "th_refresh_token";

    /// <summary>Cookie path. Scoped to the refresh/logout routes only.</summary>
    public string Path { get; set; } = "/api/staff/auth";

    /// <summary>Send only over HTTPS. Leave true outside local development.</summary>
    public bool Secure { get; set; } = true;

    /// <summary><c>Strict</c>, <c>Lax</c> or <c>None</c>. <c>None</c> requires <see cref="Secure"/>.</summary>
    public string SameSite { get; set; } = "Strict";
}
