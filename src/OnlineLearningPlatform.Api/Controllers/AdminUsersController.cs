using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Authentication;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminUsersController(IUserManagementService userManagement) : ControllerBase
{
    [HttpGet]
    [HttpGet("/api/users")]
    [ProducesResponseType(typeof(PagedAdminUsersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] AdminUserListQuery query, CancellationToken cancellationToken)
    {
        var response = await userManagement.ListUsersAsync(query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("/api/users/{id:guid}")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var profile = await userManagement.GetProfileAsync(id, cancellationToken);
        return profile is null
            ? Problem(title: "The user was not found.", statusCode: StatusCodes.Status404NotFound)
            : Ok(profile);
    }

    [HttpPut("/api/users/{id:guid}")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateById(
        Guid id, UpdateUserProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await userManagement.UpdateProfileAsync(id, request, cancellationToken);
        return profile is null
            ? Problem(title: "The user was not found.", statusCode: StatusCodes.Status404NotFound)
            : Ok(profile);
    }

    [HttpPut("{id:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        ChangeUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var outcome = await userManagement.ChangeRoleAsync(
            actorId,
            id,
            request.Role,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return MapOutcome(outcome);
    }

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        ChangeUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var outcome = await userManagement.ChangeStatusAsync(
            actorId,
            id,
            request.IsActive,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return MapOutcome(outcome);
    }

    [HttpDelete("{id:guid}")]
    [HttpDelete("/api/users/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var outcome = await userManagement.RemoveUserAsync(
            actorId,
            id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return MapOutcome(outcome);
    }

    private bool TryGetActorId(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actorId);

    private IActionResult MapOutcome(UserManagementOutcome outcome) => outcome switch
    {
        UserManagementOutcome.Succeeded => NoContent(),
        UserManagementOutcome.NotFound => Problem(title: "The user was not found.", statusCode: StatusCodes.Status404NotFound),
        UserManagementOutcome.LastActiveAdmin => Problem(
            title: "The last active administrator cannot be removed, demoted, or deactivated.",
            statusCode: StatusCodes.Status409Conflict),
        _ => Problem(title: "The user management request could not be completed.", statusCode: StatusCodes.Status500InternalServerError),
    };
}
