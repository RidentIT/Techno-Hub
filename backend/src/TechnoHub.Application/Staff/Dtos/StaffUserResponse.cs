namespace TechnoHub.Application.Staff.Dtos;

/// <summary>
/// A staff account as returned by <c>/me</c>, <c>/register</c> and the user-management endpoints.
/// </summary>
/// <param name="Id">Staff account id.</param>
/// <param name="Email">Login email.</param>
/// <param name="UserName">Login username. Defaults to the email when not supplied at creation.</param>
/// <param name="FullName">Display name.</param>
/// <param name="PhoneNumber">Optional contact number.</param>
/// <param name="Role">One of Admin, Technician, User.</param>
/// <param name="IdentityType">Always <c>staff</c>. Mirrors the <c>type</c> claim in the JWT.</param>
/// <param name="IsActive">False for a soft-disabled account, which cannot log in or refresh.</param>
/// <param name="Scopes">
/// Explicitly granted scope keys. Empty for an Admin — an Admin passes every check by role, so it
/// holds no scope rows. Use <paramref name="HasAllScopes"/> rather than inspecting this list when
/// deciding whether the user can do something.
/// </param>
/// <param name="HasAllScopes">True for Admin, which bypasses all scope checks.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="LastLoginAt">Last successful login, if any.</param>
public sealed record StaffUserResponse(
    Guid Id,
    string Email,
    string UserName,
    string FullName,
    string? PhoneNumber,
    string Role,
    string IdentityType,
    bool IsActive,
    IReadOnlyList<string> Scopes,
    bool HasAllScopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);
