using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TechnoHub.Application.Auth;
using TechnoHub.Application.Common.Options;
using TechnoHub.Domain.Constants;
using TechnoHub.Domain.Entities;

namespace TechnoHub.Infrastructure.Auth;

/// <summary>
/// Mints access and refresh tokens. Stateless — persistence of refresh tokens is the auth
/// service's job.
/// </summary>
internal sealed class TokenService : ITokenService
{
    /// <summary>Bytes of entropy in a refresh token.</summary>
    private const int RefreshTokenBytes = 64;

    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        var keyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(
        ApplicationUser user,
        string role,
        IReadOnlyCollection<string> scopes)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.Add(_options.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            // A per-token id, so an individual access token can be identified in logs.
            new("jti", Guid.NewGuid().ToString("N")),
            new("email", user.Email ?? string.Empty),
            new("name", user.UserName ?? string.Empty),
            new(ClaimTypesExtended.FullName, user.FullName),

            // Identity space. Every authorization policy asserts this before looking at role or
            // scopes, so a token minted for some future non-staff identity can never satisfy a
            // staff policy even if it carries a matching role string.
            new(ClaimTypesExtended.TokenType, IdentityTypes.Staff),

            // Short "role" claim type rather than the WS-Fed URI; the JWT bearer handler is
            // configured with RoleClaimType = "role" to match.
            new("role", role),
        };

        // One claim per scope. Baked in at issue time so authorization never needs a database hit;
        // the cost is that a scope change only lands on the next refresh, which is why the access
        // token lifetime is kept short and a scope change revokes the user's refresh tokens.
        claims.AddRange(scopes.Select(scope => new Claim(ClaimTypesExtended.Scope, scope)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    public RefreshTokenPair CreateRefreshToken()
    {
        // Opaque and random — a refresh token carries no claims, it is just a database key.
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenBytes));

        return new RefreshTokenPair(
            raw,
            HashRefreshToken(raw),
            DateTimeOffset.UtcNow.Add(_options.RefreshTokenLifetime));
    }

    public string HashRefreshToken(string rawValue)
    {
        // Plain SHA-256 is the right tool here, unlike for passwords: the input is 64 bytes of
        // cryptographic randomness, so there is nothing to brute-force and no salt to add.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToBase64String(hash);
    }
}
