using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/teacher/courses/{courseId:guid}/discussions")]
[Authorize(Policy = AuthorizationPolicies.Teacher)]
public sealed class TeacherDiscussionsController(IDiscussionService discussions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid courseId, CancellationToken ct) =>
        TryActor(out var teacher) ? Ok(await discussions.ListForTeacherAsync(courseId, teacher, ct)) : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
