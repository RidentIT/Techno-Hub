using AppRefreshCookieOptions = TechnoHub.Application.Common.Options.RefreshCookieOptions;
using HttpCookieOptions = Microsoft.AspNetCore.Http.CookieOptions;

namespace TechnoHub.Api.Extensions;

/// <summary>
/// Reads and writes the httpOnly refresh-token cookie.
/// </summary>
/// <remarks>
/// The cookie is always <c>HttpOnly</c> and scoped to the auth routes, so it is never readable from
/// JavaScript and is not attached to ordinary API calls. The admin app additionally fronts this API
/// with its own Next.js route handlers, so in that setup the browser only ever sees a cookie on the
/// Next origin — this cookie is what the BFF holds on the server side, and what Swagger uses when
/// you call the endpoints directly.
/// </remarks>
public static class RefreshCookieExtensions
{
    public static void SetRefreshCookie(
        this HttpResponse response,
        string refreshToken,
        DateTimeOffset expiresAt,
        AppRefreshCookieOptions options)
    {
        response.Cookies.Append(options.Name, refreshToken, new HttpCookieOptions
        {
            HttpOnly = true,
            Secure = options.Secure,
            SameSite = ParseSameSite(options.SameSite),
            Path = options.Path,
            Expires = expiresAt,
            IsEssential = true,
        });
    }

    public static void ClearRefreshCookie(this HttpResponse response, AppRefreshCookieOptions options)
    {
        // Delete has to repeat the same attributes it was written with, or the browser treats it as
        // a different cookie and leaves the original in place.
        response.Cookies.Delete(options.Name, new HttpCookieOptions
        {
            HttpOnly = true,
            Secure = options.Secure,
            SameSite = ParseSameSite(options.SameSite),
            Path = options.Path,
            IsEssential = true,
        });
    }

    public static string? ReadRefreshCookie(this HttpRequest request, AppRefreshCookieOptions options) =>
        request.Cookies.TryGetValue(options.Name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static SameSiteMode ParseSameSite(string value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "none" => SameSiteMode.None,
            "lax" => SameSiteMode.Lax,
            _ => SameSiteMode.Strict,
        };
}
