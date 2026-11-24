using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId:guid}/lessons")]
[Authorize(Policy = AuthorizationPolicies.Student)]
public sealed class StudentLessonsController(ILessonService lessons) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid courseId, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await lessons.ListForStudentAsync(courseId, actor, ct)) : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
