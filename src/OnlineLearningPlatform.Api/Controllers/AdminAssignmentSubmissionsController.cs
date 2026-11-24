using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/assignments")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminAssignmentSubmissionsController(IAssignmentService assignments) : ControllerBase
{
    [HttpGet("{assignmentId:guid}/submissions")]
    public async Task<IActionResult> List(Guid assignmentId, CancellationToken ct) =>
        Ok(await assignments.ListSubmissionsForAdminAsync(assignmentId, ct));
}
