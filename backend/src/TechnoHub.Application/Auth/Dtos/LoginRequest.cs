namespace TechnoHub.Application.Auth.Dtos;

/// <summary>Staff login payload.</summary>
/// <param name="EmailOrUsername">Either the account's email or its username.</param>
/// <param name="Password">The account password.</param>
public sealed record LoginRequest(string EmailOrUsername, string Password);
