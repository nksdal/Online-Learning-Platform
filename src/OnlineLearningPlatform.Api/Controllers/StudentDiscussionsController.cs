using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Student)]
public sealed class StudentDiscussionsController(IDiscussionService discussions) : ControllerBase
{
    [HttpGet("/api/courses/{courseId:guid}/discussions")]
    public async Task<IActionResult> List(Guid courseId, CancellationToken ct) =>
        TryActor(out var student) ? Ok(await discussions.ListForStudentAsync(courseId, student, ct)) : Unauthorized();

    [HttpPost("/api/courses/{courseId:guid}/discussions")]
    public async Task<IActionResult> Create(Guid courseId, DiscussionPostRequest request, CancellationToken ct)
    {
        if (!TryActor(out var student)) return Unauthorized();
        var post = await discussions.CreateAsync(courseId, student, request, ct);
        return Created($"/api/courses/{courseId}/discussions", post);
    }

    [HttpPost("/api/discussions/{postId:guid}/replies")]
    public async Task<IActionResult> Reply(Guid postId, DiscussionPostRequest request, CancellationToken ct)
    {
        if (!TryActor(out var student)) return Unauthorized();
        var reply = await discussions.ReplyAsync(postId, student, request, ct);
        return Created($"/api/discussions/{postId}/replies", reply);
    }

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);
}
