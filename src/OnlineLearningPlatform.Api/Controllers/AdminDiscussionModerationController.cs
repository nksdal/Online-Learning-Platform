using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/discussions")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminDiscussionModerationController(IDiscussionService discussions) : ControllerBase
{
    [HttpPut("{postId:guid}/moderation")]
    public async Task<IActionResult> Moderate(Guid postId, ModerateDiscussionRequest request, CancellationToken ct) =>
        TryActor(out var admin)
            ? Ok(await discussions.ModerateAsync(postId, admin, true, request, ct))
            : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
