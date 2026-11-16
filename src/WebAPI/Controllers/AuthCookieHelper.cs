using GPMH.Application.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

internal static class AuthCookieHelper
{
    public const string RefreshCookieName = "gpmh_refresh_token";
    public const string ClientTypeHeaderName = "X-Client-Type";

    public static OkObjectResult BuildAuthResponse(
        ControllerBase controller,
        AuthTokenBundleDto bundle,
        bool isMobile)
    {
        if (isMobile)
        {
            return controller.Ok(new AuthResponseDto(bundle.AccessToken, bundle.ExpiresAtUtc, bundle.User, bundle.RefreshToken));
        }

        controller.Response.Cookies.Append(
            RefreshCookieName,
            bundle.RefreshToken,
            BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(7)));

        return controller.Ok(new AuthResponseDto(bundle.AccessToken, bundle.ExpiresAtUtc, bundle.User, RefreshToken: null));
    }

    public static bool IsMobileClient(HttpRequest request, string? bodyClientType) =>
        request.Headers.TryGetValue(ClientTypeHeaderName, out Microsoft.Extensions.Primitives.StringValues headerVal) &&
        string.Equals(headerVal.ToString().Trim(), "mobile", StringComparison.OrdinalIgnoreCase)
        || string.Equals(bodyClientType?.Trim(), "mobile", StringComparison.OrdinalIgnoreCase);

    public static CookieOptions BuildCookieOptions(DateTimeOffset expiresAtUtc) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Expires = expiresAtUtc,
        };
}

