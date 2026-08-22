using TechnoHub.Application.Staff.Dtos;

namespace TechnoHub.Application.Auth.Dtos;

/// <summary>Result of a successful login or refresh.</summary>
/// <param name="AccessToken">Short-lived JWT. Carries the role and one <c>scope</c> claim per grant.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="ExpiresInSeconds">Access-token lifetime, for scheduling a silent refresh.</param>
/// <param name="AccessTokenExpiresAt">Absolute access-token expiry.</param>
/// <param name="RefreshToken">
/// Opaque rotating refresh token. Also set as an httpOnly cookie by the API; it is returned in the
/// body so that a trusted server-side caller (the admin app's BFF route handlers, or Swagger) can
/// take ownership of storing it. Browser code must never hold this value.
/// </param>
/// <param name="RefreshTokenExpiresAt">Absolute refresh-token expiry.</param>
/// <param name="User">The authenticated staff member, including role and scopes.</param>
public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    StaffUserResponse User);
