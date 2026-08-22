using TechnoHub.Application.Staff.Dtos;

namespace TechnoHub.Application.Staff;

/// <summary>Admin management of existing staff accounts.</summary>
public interface IStaffUserService
{
    /// <summary>All staff accounts, active and disabled, ordered by name.</summary>
    Task<IReadOnlyList<StaffUserResponse>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>One staff account by id.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">No such user.</exception>
    Task<StaffUserResponse> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the user's scope set and revokes their refresh tokens so the change cannot be
    /// refreshed past.
    /// </summary>
    /// <exception cref="Common.Exceptions.NotFoundException">No such user.</exception>
    /// <exception cref="Common.Exceptions.ValidationFailedException">An unknown scope key was supplied.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">
    /// The target is an Admin. Admins pass every check by role, so granting them scopes is
    /// meaningless and is rejected rather than silently ignored.
    /// </exception>
    Task<StaffUserResponse> UpdateScopesAsync(
        Guid userId,
        UpdateUserScopesRequest request,
        Guid actingUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Activates or soft-disables the account. Disabling revokes all refresh tokens.
    /// </summary>
    /// <exception cref="Common.Exceptions.NotFoundException">No such user.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">
    /// The caller is trying to disable their own account, or disable the last active Admin.
    /// </exception>
    Task<StaffUserResponse> UpdateStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        Guid actingUserId,
        CancellationToken cancellationToken);
}
