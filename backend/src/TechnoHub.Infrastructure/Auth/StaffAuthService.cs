using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TechnoHub.Application.Auth;
using TechnoHub.Application.Auth.Dtos;
using TechnoHub.Application.Common.Exceptions;
using TechnoHub.Application.Staff.Dtos;
using TechnoHub.Domain.Constants;
using TechnoHub.Domain.Entities;
using TechnoHub.Infrastructure.Persistence;
using TechnoHub.Infrastructure.Staff;

namespace TechnoHub.Infrastructure.Auth;

/// <inheritdoc />
/// <remarks>
/// Internal on purpose: everything outside this assembly consumes
/// <see cref="IStaffAuthService"/>, which lives in the Application layer.
/// </remarks>
internal sealed class StaffAuthService : IStaffAuthService
{
    private readonly TechnoHubDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly StaffUserReader _reader;
    private readonly RefreshTokenRevoker _revoker;
    private readonly ILogger<StaffAuthService> _logger;

    public StaffAuthService(
        TechnoHubDbContext db,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        StaffUserReader reader,
        RefreshTokenRevoker revoker,
        ILogger<StaffAuthService> logger)
    {
        _db = db;
        _userManager = userManager;
        _tokenService = tokenService;
        _reader = reader;
        _revoker = revoker;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var identifier = request.EmailOrUsername.Trim();
        var user = await FindByEmailOrUsernameAsync(identifier);

        if (user is null)
        {
            _logger.LogWarning("Staff login rejected: no account matches {Identifier}.", identifier);
            throw AuthenticationFailedException.InvalidCredentials();
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Staff login rejected: account {UserId} is locked out.", user.Id);
            throw new AuthenticationFailedException(
                "This account is temporarily locked after too many failed attempts. Try again later.",
                "account_locked");
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // Feeds Identity's lockout counter.
            await _userManager.AccessFailedAsync(user);
            _logger.LogWarning("Staff login rejected: wrong password for account {UserId}.", user.Id);
            throw AuthenticationFailedException.InvalidCredentials();
        }

        // Checked only after the password verifies, so this endpoint can't be used to discover
        // which accounts exist and which have been disabled.
        if (!user.IsActive)
        {
            _logger.LogWarning("Staff login rejected: account {UserId} is deactivated.", user.Id);
            throw AuthenticationFailedException.AccountDeactivated();
        }

        if (await _userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        var response = await IssueTokensAsync(user, ipAddress, cancellationToken);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Staff {UserId} ({Email}) logged in as {Role} with {ScopeCount} scope(s).",
            user.Id, user.Email, response.User.Role, response.User.Scopes.Count);

        return response;
    }

    public async Task<AuthResponse> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw AuthenticationFailedException.InvalidRefreshToken();
        }

        var hash = _tokenService.HashRefreshToken(refreshToken);

        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            _logger.LogWarning("Refresh rejected: token hash not found.");
            throw AuthenticationFailedException.InvalidRefreshToken();
        }

        if (stored.RevokedAt is not null)
        {
            // Tokens rotate on every use, so a second presentation of one that is already revoked
            // means the value leaked. Kill every live token for the account rather than just this
            // one — we can no longer tell the attacker's session from the user's.
            await RevokeActiveTokensAsync(
                stored.UserId, RefreshTokenRevocationReasons.ReuseDetected, cancellationToken);

            _logger.LogWarning(
                "Refresh rejected and all sessions revoked: an already-revoked token was replayed " +
                "for staff {UserId} (original revocation: {Reason}).",
                stored.UserId, stored.RevokedReason);

            throw AuthenticationFailedException.InvalidRefreshToken();
        }

        if (stored.IsExpired)
        {
            _logger.LogInformation("Refresh rejected: token for staff {UserId} expired at {ExpiresAt}.",
                stored.UserId, stored.ExpiresAt);
            throw AuthenticationFailedException.InvalidRefreshToken();
        }

        var user = stored.User;
        if (user is null)
        {
            _logger.LogError("Refresh token {TokenId} has no owning user.", stored.Id);
            throw AuthenticationFailedException.InvalidRefreshToken();
        }

        if (!user.IsActive)
        {
            await RevokeActiveTokensAsync(
                user.Id, RefreshTokenRevocationReasons.AccountDeactivated, cancellationToken);

            _logger.LogWarning("Refresh rejected: account {UserId} is deactivated.", user.Id);
            throw AuthenticationFailedException.AccountDeactivated();
        }

        // Rotate: the presented token dies here and is linked to its replacement, which is what
        // makes the replay detection above possible.
        var response = await IssueTokensAsync(user, ipAddress, cancellationToken, rotatedFrom: stored);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refreshed tokens for staff {UserId}; {ScopeCount} scope(s) in the new token.",
            user.Id, response.User.Scopes.Count);

        return response;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        // Idempotent by design: a client logging out should never see an error, whatever state its
        // token is in.
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = _tokenService.HashRefreshToken(refreshToken);

        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = DateTimeOffset.UtcNow;
        stored.RevokedReason = RefreshTokenRevocationReasons.Logout;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff {UserId} logged out; refresh token revoked.", stored.UserId);
    }

    public async Task<StaffUserResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw NotFoundException.For("Staff account", userId);

        return await _reader.BuildAsync(user, cancellationToken);
    }

    public async Task<StaffUserResponse> RegisterStaffAsync(
        RegisterStaffRequest request,
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        // Belt-and-braces: the validator already rejects a non-assignable role, but this is the
        // guard that actually prevents privilege escalation to Admin, so it lives here too.
        if (!RoleNames.IsAssignable(request.Role))
        {
            // Field keys are the camelCase names the client actually sent, matching what the
            // FluentValidation filter produces, so the frontend can look them up either way.
            throw new ValidationFailedException(
                "role",
                $"Role must be one of: {string.Join(", ", RoleNames.Assignable)}.");
        }

        var email = request.Email.Trim();
        var userName = string.IsNullOrWhiteSpace(request.UserName) ? email : request.UserName.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException($"A staff account with email '{email}' already exists.", "email_taken");
        }

        if (await _userManager.FindByNameAsync(userName) is not null)
        {
            throw new ConflictException($"A staff account with username '{userName}' already exists.", "username_taken");
        }

        var scopeKeys = ResolveInitialScopes(request.Role, request.Scopes);

        var unknown = scopeKeys.Where(key => !ScopeNames.IsValid(key)).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationFailedException(
                "scopes",
                $"Unknown scope(s): {string.Join(", ", unknown)}.");
        }

        // Creating the account, assigning the role and granting the scopes must be all-or-nothing:
        // a user with no role would be able to authenticate but sit outside the role model entirely.
        var strategy = _db.Database.CreateExecutionStrategy();

        var created = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = userName,
                FullName = request.FullName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
                // Staff accounts are created by an Admin who already knows the person, so there is
                // no email-confirmation round trip to run.
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = createdByUserId,
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                throw ToValidationException(createResult);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign role '{request.Role}': {Describe(roleResult)}");
            }

            await GrantScopesAsync(user.Id, scopeKeys, createdByUserId, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return user;
        });

        _logger.LogInformation(
            "Admin {AdminId} created staff account {UserId} ({Email}) with role {Role} and scopes {Scopes}.",
            createdByUserId, created.Id, created.Email, request.Role, scopeKeys);

        return StaffUserReader.Project(created, request.Role, scopeKeys);
    }

    /// <summary>
    /// Accepts either form of identifier. Tries the likelier store first based on whether the input
    /// looks like an email, then falls back, so <c>admin@x.com</c> works as a username too.
    /// </summary>
    private async Task<ApplicationUser?> FindByEmailOrUsernameAsync(string identifier) =>
        identifier.Contains('@', StringComparison.Ordinal)
            ? await _userManager.FindByEmailAsync(identifier) ?? await _userManager.FindByNameAsync(identifier)
            : await _userManager.FindByNameAsync(identifier) ?? await _userManager.FindByEmailAsync(identifier);

    /// <summary>
    /// Mints an access/refresh pair and stages the new refresh row. Role and scopes are read from
    /// the database on every call, so refresh is the point where an Admin's scope change reaches an
    /// already-signed-in user. Does not call SaveChanges — the caller commits.
    /// </summary>
    private async Task<AuthResponse> IssueTokensAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken,
        RefreshToken? rotatedFrom = null)
    {
        var role = await _reader.GetRoleAsync(user.Id, cancellationToken);
        var scopes = await _reader.GetScopeKeysAsync(user.Id, cancellationToken);

        var access = _tokenService.CreateAccessToken(user, role, scopes);
        var refresh = _tokenService.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refresh.Hash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = refresh.ExpiresAt,
            CreatedByIp = ipAddress,
        });

        if (rotatedFrom is not null)
        {
            rotatedFrom.RevokedAt = DateTimeOffset.UtcNow;
            rotatedFrom.RevokedReason = RefreshTokenRevocationReasons.Rotated;
            rotatedFrom.ReplacedByTokenHash = refresh.Hash;
        }

        return new AuthResponse(
            AccessToken: access.Token,
            TokenType: "Bearer",
            ExpiresInSeconds: (int)Math.Max(0, (access.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds),
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: refresh.RawValue,
            RefreshTokenExpiresAt: refresh.ExpiresAt,
            User: StaffUserReader.Project(user, role, scopes));
    }

    /// <summary>
    /// A <c>null</c> scope list means "use the role's default"; an explicit empty list means
    /// "no scopes at all". Technician is the only role with a non-empty default, and those are
    /// written as ordinary grants so an Admin can revoke them later.
    /// </summary>
    private static IReadOnlyList<string> ResolveInitialScopes(string role, IReadOnlyList<string>? requested)
    {
        if (requested is not null)
        {
            return requested.Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
        }

        return string.Equals(role, RoleNames.Technician, StringComparison.Ordinal)
            ? ScopeNames.TechnicianDefaults.OrderBy(k => k, StringComparer.Ordinal).ToList()
            : Array.Empty<string>();
    }

    /// <summary>Stages UserScopes rows for the given keys. Does not call SaveChanges.</summary>
    private async Task GrantScopesAsync(
        Guid userId,
        IReadOnlyList<string> scopeKeys,
        Guid grantedByUserId,
        CancellationToken cancellationToken)
    {
        if (scopeKeys.Count == 0)
        {
            return;
        }

        var rows = await _db.Scopes
            .Where(s => scopeKeys.Contains(s.Key))
            .Select(s => new { s.Id, s.Key })
            .ToListAsync(cancellationToken);

        if (rows.Count != scopeKeys.Count)
        {
            var missing = scopeKeys.Except(rows.Select(r => r.Key), StringComparer.Ordinal);
            throw new InvalidOperationException(
                $"Scope(s) {string.Join(", ", missing)} are defined in code but missing from the " +
                "Scopes table. Restart the API so the seeder can sync the catalogue.");
        }

        foreach (var row in rows)
        {
            _db.UserScopes.Add(new UserScope
            {
                UserId = userId,
                ScopeId = row.Id,
                GrantedAt = DateTimeOffset.UtcNow,
                GrantedByUserId = grantedByUserId,
            });
        }
    }

    /// <summary>
    /// Revokes every still-live refresh token for a user and commits, because both callers are
    /// about to throw and must not lose the revocation.
    /// </summary>
    private async Task RevokeActiveTokensAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        await _revoker.RevokeActiveAsync(userId, reason, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ValidationFailedException ToValidationException(IdentityResult result)
    {
        // Identity reports failures as codes like DuplicateEmail / PasswordTooShort. Bucket them
        // onto the request field they belong to so the frontend can show them inline.
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var error in result.Errors)
        {
            var field = error.Code switch
            {
                var c when c.Contains("Password", StringComparison.Ordinal) => "password",
                var c when c.Contains("Email", StringComparison.Ordinal) => "email",
                var c when c.Contains("UserName", StringComparison.Ordinal) => "userName",
                _ => "request",
            };

            if (!errors.TryGetValue(field, out var messages))
            {
                messages = new List<string>();
                errors[field] = messages;
            }

            messages.Add(error.Description);
        }

        return new ValidationFailedException(
            errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.Ordinal));
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
