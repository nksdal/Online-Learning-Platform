using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize(Policy = AuthorizationPolicies.Teacher)]
public sealed class TeacherLessonsController(ILessonService lessons) : ControllerBase
{
    [HttpGet("courses/{courseId:guid}/lessons")]
    public async Task<IActionResult> List(Guid courseId, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await lessons.ListForTeacherAsync(courseId, actor, ct)) : Unauthorized();

    [HttpPost("courses/{courseId:guid}/lessons")]
    [ProducesResponseType(typeof(LessonResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid courseId, LessonWriteRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        var lesson = await lessons.CreateAsync(courseId, actor, request, ct);
        return Created($"/api/teacher/courses/{courseId}/lessons", lesson);
    }

    [HttpPut("lessons/{lessonId:guid}")]
    public async Task<IActionResult> Update(Guid lessonId, LessonWriteRequest request, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await lessons.UpdateAsync(lessonId, actor, request, ct)) : Unauthorized();

    [HttpDelete("lessons/{lessonId:guid}")]
    public async Task<IActionResult> Delete(Guid lessonId, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        await lessons.DeleteAsync(lessonId, actor, ct);
        return NoContent();
    }

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
