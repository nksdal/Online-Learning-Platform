using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/submissions")]
[Authorize(Policy = AuthorizationPolicies.Teacher)]
public sealed class TeacherGradingController(IAssignmentService assignments) : ControllerBase
{
    [HttpPut("{submissionId:guid}/grade")]
    [ProducesResponseType(typeof(AssignmentSubmissionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Grade(Guid submissionId, GradeSubmissionRequest request, CancellationToken ct) =>
        TryActor(out var teacher)
            ? Ok(await assignments.GradeAsync(submissionId, teacher, request, ct))
            : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
