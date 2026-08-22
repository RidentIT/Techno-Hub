namespace TechnoHub.Application.Staff.Dtos;

/// <summary>
/// Activates or soft-disables a staff account. Staff are never hard-deleted, so records that
/// reference them stay intact. Deactivating revokes all of the user's refresh tokens immediately.
/// </summary>
/// <param name="IsActive">True to activate, false to disable.</param>
/// <param name="Reason">Optional note recorded in the audit log line.</param>
public sealed record UpdateUserStatusRequest(bool IsActive, string? Reason);
