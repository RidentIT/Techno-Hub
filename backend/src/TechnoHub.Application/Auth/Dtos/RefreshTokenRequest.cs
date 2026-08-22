namespace TechnoHub.Application.Auth.Dtos;

/// <summary>
/// Body for refresh and logout. Optional: when omitted, the API falls back to the httpOnly
/// refresh cookie, which is how a browser-facing caller should use these endpoints.
/// </summary>
/// <param name="RefreshToken">The raw refresh token value.</param>
public sealed record RefreshTokenRequest(string? RefreshToken);
