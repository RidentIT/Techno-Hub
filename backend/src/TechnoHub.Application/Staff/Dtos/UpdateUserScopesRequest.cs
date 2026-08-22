namespace TechnoHub.Application.Staff.Dtos;

/// <summary>
/// Replaces a user's entire scope set with <paramref name="Scopes"/>. Send an empty array to strip
/// all permissions. Every existing refresh token for that user is revoked, so the change takes
/// effect as soon as their current access token expires and cannot be refreshed past.
/// </summary>
/// <param name="Scopes">The complete list of scope keys the user should hold.</param>
public sealed record UpdateUserScopesRequest(IReadOnlyList<string> Scopes);
