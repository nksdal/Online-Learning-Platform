using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Student)]
public sealed class StudentProgressController(IProgressService progress) : ControllerBase
{
    [HttpGet("/api/courses/{courseId:guid}/progress/me")]
    [ProducesResponseType(typeof(CourseProgressResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourseProgress(Guid courseId, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await progress.GetCourseProgressAsync(courseId, actor, ct)) : Unauthorized();

    [HttpPut("/api/lessons/{lessonId:guid}/progress")]
    [ProducesResponseType(typeof(LessonCompletionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteLesson(Guid lessonId, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await progress.CompleteLessonAsync(lessonId, actor, ct)) : Unauthorized();

    [HttpDelete("/api/lessons/{lessonId:guid}/progress")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetLesson(Guid lessonId, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        await progress.DeleteLessonCompletionAsync(lessonId, actor, ct);
        return NoContent();
    }

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
