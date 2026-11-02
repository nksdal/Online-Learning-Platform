using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/teacher/discussions")]
[Authorize(Policy = AuthorizationPolicies.Teacher)]
public sealed class TeacherDiscussionModerationController(IDiscussionService discussions) : ControllerBase
{
    [HttpPut("{postId:guid}/moderation")]
    public async Task<IActionResult> Moderate(Guid postId, ModerateDiscussionRequest request, CancellationToken ct) =>
        TryActor(out var teacher)
            ? Ok(await discussions.ModerateAsync(postId, teacher, false, request, ct))
            : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
