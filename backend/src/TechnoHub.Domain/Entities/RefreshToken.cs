using System.ComponentModel.DataAnnotations.Schema;

namespace TechnoHub.Domain.Entities;

/// <summary>
/// A server-side refresh token. Persisted (rather than stateless) so that logout and
/// administrative deactivation revoke access for real.
/// </summary>
/// <remarks>
/// Only a SHA-256 hash of the token is stored. The raw value is handed to the client once and
/// never kept, so a leaked database dump cannot be replayed against the refresh endpoint.
/// </remarks>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    /// <summary>Base64 SHA-256 hash of the raw token value.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Why the token was revoked — logout, rotation, reuse-detected, deactivated.</summary>
    public string? RevokedReason { get; set; }

    /// <summary>Hash of the token that superseded this one during rotation.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    [NotMapped]
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    [NotMapped]
    public bool IsActive => RevokedAt is null && !IsExpired;
}

/// <summary>Reasons recorded in <see cref="RefreshToken.RevokedReason"/>.</summary>
public static class RefreshTokenRevocationReasons
{
    public const string Logout = "logout";
    public const string Rotated = "rotated";
    public const string ReuseDetected = "reuse-detected";
    public const string AccountDeactivated = "account-deactivated";
    public const string ScopesChanged = "scopes-changed";
}
