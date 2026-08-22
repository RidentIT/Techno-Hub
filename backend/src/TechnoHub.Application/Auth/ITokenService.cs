using TechnoHub.Domain.Entities;

namespace TechnoHub.Application.Auth;

/// <summary>A freshly minted access token and its expiry.</summary>
/// <param name="Token">The signed JWT.</param>
/// <param name="ExpiresAt">Absolute expiry.</param>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>A freshly minted refresh token: the raw value plus the hash to persist.</summary>
/// <param name="RawValue">Returned to the caller once, never stored.</param>
/// <param name="Hash">SHA-256 of <paramref name="RawValue"/>, safe to persist.</param>
/// <param name="ExpiresAt">Absolute expiry.</param>
public sealed record RefreshTokenPair(string RawValue, string Hash, DateTimeOffset ExpiresAt);

/// <summary>Creates and hashes tokens. No database access.</summary>
public interface ITokenService
{
    /// <summary>
    /// Builds a JWT carrying sub, email, name, the <c>type=staff</c> claim, the role and one
    /// <c>scope</c> claim per granted scope. Scopes are baked in so authorization never needs a
    /// database round trip.
    /// </summary>
    AccessToken CreateAccessToken(ApplicationUser user, string role, IReadOnlyCollection<string> scopes);

    /// <summary>Generates a cryptographically random refresh token and its hash.</summary>
    RefreshTokenPair CreateRefreshToken();

    /// <summary>Hashes a raw refresh token so it can be looked up against stored hashes.</summary>
    string HashRefreshToken(string rawValue);
}
