using TechnoHub.Application.Auth.Dtos;
using TechnoHub.Application.Staff.Dtos;

namespace TechnoHub.Application.Auth;

/// <summary>
/// Staff authentication. Implemented in the Infrastructure layer, which owns the Identity stores
/// and the database.
/// </summary>
public interface IStaffAuthService
{
    /// <summary>
    /// Validates credentials and issues an access/refresh token pair.
    /// </summary>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">
    /// Credentials are wrong, or the account is soft-disabled or locked out.
    /// </exception>
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a valid refresh token for a fresh pair, rotating the refresh token. Scopes are
    /// re-read from the database here, so this is the point at which a scope change reaches a
    /// signed-in user.
    /// </summary>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">
    /// The token is unknown, expired, already rotated or revoked, or the account is disabled.
    /// </exception>
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the supplied refresh token. Idempotent: an unknown or already-revoked token is a
    /// no-op rather than an error, so logout never fails for the client.
    /// </summary>
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);

    /// <summary>Profile, role and scopes of the authenticated caller, read fresh from the database.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">The user id no longer exists.</exception>
    Task<StaffUserResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a Technician or User account on behalf of an Admin.
    /// </summary>
    /// <exception cref="Common.Exceptions.ConflictException">Email or username is already taken.</exception>
    /// <exception cref="Common.Exceptions.ValidationFailedException">
    /// The role is not assignable, a scope key is unknown, or the password fails Identity's policy.
    /// </exception>
    Task<StaffUserResponse> RegisterStaffAsync(
        RegisterStaffRequest request,
        Guid createdByUserId,
        CancellationToken cancellationToken);
}
