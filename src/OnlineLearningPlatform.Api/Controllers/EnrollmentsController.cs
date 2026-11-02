using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId:guid}/enrollments")]
[Authorize(Policy = AuthorizationPolicies.Student)]
public sealed class EnrollmentsController(IEnrollmentService enrollments) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(EnrollmentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Enroll(Guid courseId, CancellationToken ct)
    {
        if (!TryActor(out var studentId)) return Unauthorized();
        var enrollment = await enrollments.EnrollAsync(courseId, studentId, ct);
        return Created("/api/users/me/enrollments", enrollment);
    }

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
