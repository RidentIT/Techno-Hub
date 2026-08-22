namespace TechnoHub.Domain.Constants;

/// <summary>
/// Custom claim types baked into every issued access token.
/// </summary>
public static class ClaimTypesExtended
{
    /// <summary>
    /// Identity space of the token. Always <see cref="IdentityTypes.Staff"/> today, but every
    /// authorization policy asserts it before looking at role or scopes. If a second identity
    /// space is ever introduced, tokens from it cannot satisfy a staff policy by accident.
    /// </summary>
    public const string TokenType = "type";

    /// <summary>One claim per granted scope key.</summary>
    public const string Scope = "scope";

    /// <summary>Display name of the staff member.</summary>
    public const string FullName = "full_name";
}

public static class IdentityTypes
{
    public const string Staff = "staff";
}
