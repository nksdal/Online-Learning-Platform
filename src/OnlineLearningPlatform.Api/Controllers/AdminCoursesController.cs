using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/courses")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminCoursesController(ICourseService courses, ILessonService lessons, IAssignmentService assignments) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<CourseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAll(CancellationToken ct) => Ok(await courses.ListAllAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CourseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await courses.GetAnyAsync(id, ct));

    [HttpGet("pending")]
    public async Task<IActionResult> ListPending(CancellationToken ct) => Ok(await courses.ListPendingAsync(ct));

    [HttpGet("{id:guid}/lessons")]
    public async Task<IActionResult> ListLessons(Guid id, CancellationToken ct) => Ok(await lessons.ListForAdminAsync(id, ct));

    [HttpGet("{id:guid}/assignments")]
    public async Task<IActionResult> ListAssignments(Guid id, CancellationToken ct) => Ok(await assignments.ListForAdminAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await courses.ApproveAsync(actor, id, ct)) : Unauthorized();

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CourseRejectionRequest request, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await courses.RejectAsync(actor, id, request.Reason, ct)) : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
