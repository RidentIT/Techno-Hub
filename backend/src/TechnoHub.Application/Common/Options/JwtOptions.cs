namespace TechnoHub.Application.Common.Options;

/// <summary>
/// Access-token signing and lifetime settings, bound from the <c>Jwt</c> configuration section.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TechnoHub.Api";

    public string Audience { get; set; } = "TechnoHub.Staff";

    /// <summary>
    /// HMAC-SHA256 signing key. Must be at least 32 bytes. Supplied via environment variable
    /// (<c>Jwt__SigningKey</c>) or user-secrets — never committed to appsettings.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Access-token lifetime. Kept short because scopes are baked into the token: a scope change
    /// only takes effect on the next refresh, so this value is the worst-case staleness window.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 45;

    /// <summary>Refresh-token lifetime. Rotated on every use.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>Tolerance for clock drift when validating token lifetime.</summary>
    public int ClockSkewSeconds { get; set; } = 30;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);
}
