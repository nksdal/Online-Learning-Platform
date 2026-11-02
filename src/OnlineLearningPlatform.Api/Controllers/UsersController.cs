using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Authentication;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[EnableRateLimiting(AuthRateLimitPolicy.Name)]
public sealed class UsersController(
    IAuthService authService,
    IUserManagementService userManagement,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await userManagement.GetProfileAsync(userId, cancellationToken);
        return profile is null
            ? Problem(title: "The account was not found.", statusCode: StatusCodes.Status404NotFound)
            : Ok(profile);
    }

    [HttpPost("me/password")]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var updatedAccessToken = await authService.ChangePasswordAsync(userId, request, cancellationToken);
        if (updatedAccessToken is null)
        {
            return Problem(title: "The current password is incorrect or the password change was rejected.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        Response.Cookies.Delete("olp_refresh", new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
        });
        return Ok(updatedAccessToken);
    }
}
