namespace TechnoHub.Application.Auth.Dtos;

/// <summary>
/// Admin-only payload for creating a staff account. There is no public self-registration in this
/// system, so this is the only way an account comes into existence besides the seeded root Admin.
/// </summary>
/// <param name="Email">Login email. Must be unique.</param>
/// <param name="UserName">Optional login username. Defaults to <paramref name="Email"/>.</param>
/// <param name="FullName">Display name.</param>
/// <param name="PhoneNumber">Optional contact number.</param>
/// <param name="Password">Initial password.</param>
/// <param name="Role">
/// <c>Technician</c> or <c>User</c>. Creating another Admin through this endpoint is not allowed.
/// </param>
/// <param name="Scopes">
/// Initial scope keys. A <c>Technician</c> with no scopes supplied is seeded with
/// <c>repairs.view</c> and <c>repairs.manage</c>; pass an explicit list to override that. A
/// <c>User</c> with no scopes supplied gets none, and can do nothing until an Admin grants some.
/// </param>
public sealed record RegisterStaffRequest(
    string Email,
    string? UserName,
    string FullName,
    string? PhoneNumber,
    string Password,
    string Role,
    IReadOnlyList<string>? Scopes);
