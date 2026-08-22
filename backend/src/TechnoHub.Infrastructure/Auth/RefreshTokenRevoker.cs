using Microsoft.EntityFrameworkCore;
using TechnoHub.Infrastructure.Persistence;

namespace TechnoHub.Infrastructure.Auth;

/// <summary>
/// Revokes a user's live refresh tokens. Shared by logout-style flows in the auth service and by
/// the admin actions that must cut an existing session short — a scope change or a deactivation.
/// </summary>
/// <remarks>
/// Stages the changes only; the caller decides when to commit, so a revocation can take part in the
/// same transaction as whatever caused it.
/// </remarks>
internal sealed class RefreshTokenRevoker
{
    private readonly TechnoHubDbContext _db;

    public RefreshTokenRevoker(TechnoHubDbContext db)
    {
        _db = db;
    }

    /// <returns>How many tokens were revoked.</returns>
    public async Task<int> RevokeActiveAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;
        }

        return tokens.Count;
    }
}
