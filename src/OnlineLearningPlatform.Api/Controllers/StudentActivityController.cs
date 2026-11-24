using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/users/me")]
[Authorize(Policy = AuthorizationPolicies.Student)]
public sealed class StudentActivityController(IStreakService streaks, IRecommendationService recommendations) : ControllerBase
{
    [HttpGet("streak")]
    public async Task<IActionResult> GetStreak(CancellationToken ct) =>
        TryActor(out var student) ? Ok(await streaks.GetMyStreakAsync(student, ct)) : Unauthorized();

    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations([FromQuery] int limit = 10, CancellationToken ct = default) =>
        TryActor(out var student) ? Ok(await recommendations.RecommendAsync(student, limit, ct)) : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
