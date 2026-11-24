using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminAnalyticsController(IAdminAnalyticsService analytics) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AdminAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int days = 30, CancellationToken ct = default)
    {
        if (days is not (7 or 30 or 90 or 365))
            return Problem(title: "Choose a 7, 30, 90, or 365 day reporting range.",
                statusCode: StatusCodes.Status400BadRequest);
        try
        {
            return Ok(await analytics.GetAsync(days, ct));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Problem(title: "Choose a 7, 30, 90, or 365 day reporting range.",
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
