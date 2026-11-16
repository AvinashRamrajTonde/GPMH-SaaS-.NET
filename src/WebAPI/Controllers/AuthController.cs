using GPMH.Application.DTOs.Auth;
using GPMH.Application.Features.Auth.Commands;
using GPMH.Application.Features.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    public const string RefreshCookieName = AuthCookieHelper.RefreshCookieName;
    public const string ClientTypeHeaderName = AuthCookieHelper.ClientTypeHeaderName;

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthTokenBundleDto bundle = await sender
            .Send(new LoginCommand(request.Username, request.Password, request.TenantId), cancellationToken)
            .ConfigureAwait(false);

        return AuthCookieHelper.BuildAuthResponse(this, bundle, AuthCookieHelper.IsMobileClient(Request, request.ClientType));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponseDto>> Refresh(
        [FromBody] RefreshRequest? request,
        CancellationToken cancellationToken)
    {
        bool isMobile = AuthCookieHelper.IsMobileClient(Request, request?.ClientType);
        string? rawRefreshToken = Request.Cookies.TryGetValue(RefreshCookieName, out string? cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken)
            ? cookieToken
            : request?.RefreshToken;

        AuthTokenBundleDto bundle = await sender
            .Send(new RefreshTokenCommand(rawRefreshToken ?? string.Empty, request?.TenantId), cancellationToken)
            .ConfigureAwait(false);

        return AuthCookieHelper.BuildAuthResponse(this, bundle, isMobile);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        string? rawRefreshToken = Request.Cookies.TryGetValue(RefreshCookieName, out string? cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken)
            ? cookieToken
            : request?.RefreshToken;

        await sender.Send(new LogoutCommand(rawRefreshToken), cancellationToken).ConfigureAwait(false);

        Response.Cookies.Delete(RefreshCookieName, AuthCookieHelper.BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(-1)));
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthenticatedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUserDto>> Me(CancellationToken cancellationToken)
    {
        AuthenticatedUserDto current = await sender
            .Send(new GetCurrentUserQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(current);
    }
}

public sealed record LoginRequest(
    string Username,
    string Password,
    Guid? TenantId = null,
    string? ClientType = null);

public sealed record RefreshRequest(
    string? RefreshToken = null,
    Guid? TenantId = null,
    string? ClientType = null);

public sealed record LogoutRequest(string? RefreshToken = null);

