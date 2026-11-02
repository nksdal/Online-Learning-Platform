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
public sealed class TeacherAssignmentsController(IAssignmentService assignments) : ControllerBase
{
    [HttpGet("courses/{courseId:guid}/assignments")]
    public async Task<IActionResult> ListForCourse(Guid courseId, CancellationToken ct) =>
        TryActor(out var teacher) ? Ok(await assignments.ListForTeacherAsync(courseId, teacher, ct)) : Unauthorized();

    [HttpPost("courses/{courseId:guid}/assignments")]
    [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid courseId, AssignmentWriteRequest request, CancellationToken ct)
    {
        if (!TryActor(out var teacher)) return Unauthorized();
        var assignment = await assignments.CreateAsync(courseId, teacher, request, ct);
        return Created($"/api/teacher/courses/{courseId}/assignments", assignment);
    }

    [HttpPut("assignments/{assignmentId:guid}")]
    public async Task<IActionResult> Update(Guid assignmentId, AssignmentWriteRequest request, CancellationToken ct) =>
        TryActor(out var teacher) ? Ok(await assignments.UpdateAsync(assignmentId, teacher, request, ct)) : Unauthorized();

    [HttpGet("assignments/{assignmentId:guid}/submissions")]
    public async Task<IActionResult> ListSubmissions(Guid assignmentId, CancellationToken ct) =>
        TryActor(out var teacher) ? Ok(await assignments.ListSubmissionsForTeacherAsync(assignmentId, teacher, ct)) : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
