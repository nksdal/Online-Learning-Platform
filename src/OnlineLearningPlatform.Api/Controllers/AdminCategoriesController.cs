using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminCategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await categories.ListAllAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CategoryWriteRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var category = await categories.CreateAsync(
            actorId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            request,
            cancellationToken);
        return Created("/api/categories", category);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        CategoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(await categories.UpdateAsync(
            actorId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            id,
            request,
            cancellationToken));
    }

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        Guid id,
        CategoryStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
        {
            return Problem(title: "The access token subject is invalid.", statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(await categories.SetActiveAsync(
            actorId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            id,
            request.IsActive,
            cancellationToken));
    }

    private bool TryGetActorId(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actorId);
}
