using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Authentication;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(AuthRateLimitPolicy.Name)]
public sealed class AuthController(IAuthService authService, IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookieName = "olp_refresh";
    private const string RefreshRequestHeader = "X-Refresh-Request";
    private const string GenericMessage =
        "If the request can be completed, the next steps will be sent to the supplied email address.";

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        await authService.RegisterAsync(request, cancellationToken);
        return Accepted(new AuthMessageResponse(GenericMessage));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await authService.VerifyEmailAsync(request, cancellationToken);
        return Ok(new AuthMessageResponse("If the verification details are valid, the email address is confirmed."));
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendVerification(
        ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        await authService.ResendVerificationAsync(request, cancellationToken);
        return Accepted(new AuthMessageResponse(GenericMessage));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.ForgotPasswordAsync(request, cancellationToken);
        return Accepted(new AuthMessageResponse(GenericMessage));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var succeeded = await authService.ResetPasswordAsync(request, cancellationToken);
        return succeeded
            ? Ok(new AuthMessageResponse("Your password has been reset. You can now log in with the new password."))
            : Problem(title: "The password reset details are invalid or expired.", statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.LoginAsync(request, GetRemoteIp(), GetUserAgent(), cancellationToken);
        if (session is null)
        {
            return Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
        }

        SetRefreshCookie(session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        return Ok(new AccessTokenResponse(session.AccessToken, session.AccessTokenExpiresAtUtc));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromHeader(Name = RefreshRequestHeader)] string? refreshRequest,
        CancellationToken cancellationToken)
    {
        if (!HasRefreshRequestHeader(refreshRequest))
        {
            return Problem(title: $"The {RefreshRequestHeader} header is required.", statusCode: StatusCodes.Status403Forbidden);
        }

        Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
        var session = await authService.RefreshAsync(refreshToken, GetRemoteIp(), GetUserAgent(), cancellationToken);
        if (session is null)
        {
            ClearRefreshCookie();
            return Problem(title: "The refresh token is invalid, expired, or revoked.", statusCode: StatusCodes.Status401Unauthorized);
        }

        SetRefreshCookie(session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        return Ok(new AccessTokenResponse(session.AccessToken, session.AccessTokenExpiresAtUtc));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromHeader(Name = RefreshRequestHeader)] string? refreshRequest,
        CancellationToken cancellationToken)
    {
        if (!HasRefreshRequestHeader(refreshRequest))
        {
            return Problem(title: $"The {RefreshRequestHeader} header is required.", statusCode: StatusCodes.Status403Forbidden);
        }

        Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
        await authService.LogoutAsync(refreshToken, cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(
        [FromHeader(Name = RefreshRequestHeader)] string? refreshRequest,
        CancellationToken cancellationToken)
    {
        if (!HasRefreshRequestHeader(refreshRequest))
        {
            return Problem(title: $"The {RefreshRequestHeader} header is required.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        await authService.LogoutAllAsync(userId, cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    private static bool HasRefreshRequestHeader(string? value) => value == "1";

    private string? GetRemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() => Request.Headers.UserAgent.ToString();

    private void SetRefreshCookie(string token, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
        });
    }
}
