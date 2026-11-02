using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/teacher/courses")]
[Authorize(Policy = AuthorizationPolicies.Teacher)]
public sealed class TeacherCoursesController(ICourseService courses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        return TryActor(out var actor) ? Ok(await courses.ListInstructorAsync(actor, ct)) : Unauthorized();
    }

    [HttpPost]
    [ProducesResponseType(typeof(CourseResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CourseWriteRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        var course = await courses.CreateAsync(actor, request, ct);
        return CreatedAtAction(nameof(List), new { id = course.Id }, course);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CourseWriteRequest request, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await courses.UpdateAsync(actor, id, request, ct)) : Unauthorized();

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await courses.SubmitAsync(actor, id, ct)) : Unauthorized();

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await courses.ArchiveAsync(actor, id, ct)) : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
