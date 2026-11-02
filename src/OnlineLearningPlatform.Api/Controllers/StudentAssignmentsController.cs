using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Student)]
public sealed class StudentAssignmentsController(IAssignmentService assignments) : ControllerBase
{
    [HttpGet("/api/courses/{courseId:guid}/assignments")]
    public async Task<IActionResult> ListForCourse(Guid courseId, CancellationToken ct) =>
        TryActor(out var student) ? Ok(await assignments.ListForStudentAsync(courseId, student, ct)) : Unauthorized();

    [HttpPost("/api/assignments/{assignmentId:guid}/submissions")]
    [ProducesResponseType(typeof(AssignmentSubmissionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Submit(Guid assignmentId, AssignmentSubmissionRequest request, CancellationToken ct)
    {
        if (!TryActor(out var student)) return Unauthorized();
        var response = await assignments.SubmitAsync(assignmentId, student, request, ct);
        return Created($"/api/assignments/{assignmentId}/submissions/me", response);
    }

    [HttpGet("/api/assignments/{assignmentId:guid}/submissions/me")]
    public async Task<IActionResult> ListMine(Guid assignmentId, CancellationToken ct) =>
        TryActor(out var student) ? Ok(await assignments.ListMineAsync(assignmentId, student, ct)) : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
