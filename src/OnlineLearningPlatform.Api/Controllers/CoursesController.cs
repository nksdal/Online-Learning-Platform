using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/courses")]
[AllowAnonymous]
public sealed class CoursesController(ICourseService courses) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<CourseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? categoryId, CancellationToken cancellationToken) =>
        Ok(await courses.ListPublishedAsync(categoryId, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CourseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await courses.GetPublishedAsync(id, cancellationToken));
}
