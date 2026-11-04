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
    private const long MaxProfileImageBytes = 5 * 1024 * 1024;

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

    [HttpPut("me/profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMyProfile(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await userManagement.UpdateProfileAsync(userId, request, cancellationToken);
        return profile is null
            ? Problem(title: "The account was not found.", statusCode: StatusCodes.Status404NotFound)
            : Ok(profile);
    }

    [HttpPut("me/profile-picture")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> UploadMyProfilePicture(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        if (file is null || file.Length == 0)
        {
            return Problem(title: "Choose an image to upload.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxProfileImageBytes)
        {
            return Problem(title: "Profile images must be 5 MB or smaller.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        await using var imageStream = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var read = await imageStream.ReadAsync(header.AsMemory(bytesRead), cancellationToken);
            if (read == 0) break;
            bytesRead += read;
        }
        imageStream.Position = 0;
        if (!IsSupportedImage(header, bytesRead))
        {
            return Problem(title: "Upload a JPEG, PNG, or WebP image.", statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        try
        {
            var profile = await userManagement.UploadProfileImageAsync(
                userId, file.FileName, imageStream, cancellationToken);
            return profile is null
                ? Problem(title: "The account was not found.", statusCode: StatusCodes.Status404NotFound)
                : Ok(profile);
        }
        catch (OnlineLearningPlatform.Application.Abstractions.ProfileImageStorageNotConfiguredException)
        {
            return Problem(
                title: "Profile image uploads are not configured yet.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpDelete("me/profile-picture")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveMyProfilePicture(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await userManagement.RemoveProfileImageAsync(userId, cancellationToken);
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

    private static bool IsSupportedImage(byte[] header, int length)
    {
        var jpeg = length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var png = length >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        });
        var webp = length >= 12 &&
                   header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
                   header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';

        return jpeg || png || webp;
    }
}
