namespace TechnoHub.Application.Common.Options;

/// <summary>
/// The single root Admin created on first run, bound from the <c>SeedAdmin</c> section.
/// </summary>
/// <remarks>
/// There is no public self-registration anywhere in this system, so this account is the only way
/// to bootstrap access. Credentials must come from the environment
/// (<c>SeedAdmin__Email</c> / <c>SeedAdmin__Password</c>) — the seeder refuses to invent a
/// default password, and skips seeding entirely rather than creating a guessable account.
/// </remarks>
public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string? Email { get; set; }

    /// <summary>Optional. Defaults to the email when not supplied.</summary>
    public string? UserName { get; set; }

    public string? FullName { get; set; }

    public string? Password { get; set; }

    /// <summary>
    /// True only when both email and password were supplied. When false the seeder logs a
    /// warning and creates roles and scopes but no Admin account.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
}
