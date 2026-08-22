namespace TechnoHub.Application.Common.Exceptions;

/// <summary>
/// Base class for expected, client-facing failures. The API's exception middleware turns these
/// into ProblemDetails with <see cref="StatusCode"/>; anything that is not an
/// <see cref="AppException"/> is treated as an unhandled 500 and logged as an error.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public abstract int StatusCode { get; }

    /// <summary>Stable machine-readable code the frontend can branch on.</summary>
    public string ErrorCode { get; }
}

/// <summary>The requested resource does not exist. 404.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message, string errorCode = "not_found")
        : base(message, errorCode)
    {
    }

    public static NotFoundException For(string resource, object id) =>
        new($"{resource} '{id}' was not found.");

    public override int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>The request conflicts with existing state, e.g. a duplicate email. 409.</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message, string errorCode = "conflict")
        : base(message, errorCode)
    {
    }

    public override int StatusCode => StatusCodes.Status409Conflict;
}

/// <summary>
/// Credentials or refresh token were rejected. 401.
/// </summary>
/// <remarks>
/// The message is deliberately vague for bad credentials so the endpoint cannot be used to
/// enumerate which staff emails exist.
/// </remarks>
public sealed class AuthenticationFailedException : AppException
{
    public AuthenticationFailedException(string message, string errorCode = "authentication_failed")
        : base(message, errorCode)
    {
    }

    public static AuthenticationFailedException InvalidCredentials() =>
        new("Invalid credentials.", "invalid_credentials");

    public static AuthenticationFailedException InvalidRefreshToken() =>
        new("The refresh token is invalid, expired or already used.", "invalid_refresh_token");

    public static AuthenticationFailedException AccountDeactivated() =>
        new("This staff account has been deactivated.", "account_deactivated");

    public override int StatusCode => StatusCodes.Status401Unauthorized;
}

/// <summary>Authenticated, but not permitted. 403.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message, string errorCode = "forbidden")
        : base(message, errorCode)
    {
    }

    public override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>Request payload failed validation. 400, with per-field messages.</summary>
public sealed class ValidationFailedException : AppException
{
    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", "validation_failed")
    {
        Errors = errors;
    }

    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>
/// Local copy of the handful of status codes used above, so the Application layer does not need
/// a reference to ASP.NET Core.
/// </summary>
internal static class StatusCodes
{
    internal const int Status400BadRequest = 400;
    internal const int Status401Unauthorized = 401;
    internal const int Status403Forbidden = 403;
    internal const int Status404NotFound = 404;
    internal const int Status409Conflict = 409;
}
