using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/submissions")]
[Authorize]
public sealed class SubmissionAttachmentsController(
    IAssignmentService assignments,
    IMediaAssetStorage mediaStorage,
    IHttpClientFactory httpClientFactory,
    ILogger<SubmissionAttachmentsController> logger) : ControllerBase
{
    [HttpGet("{submissionId:guid}/attachment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid submissionId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actor))
            return Unauthorized();

        SubmissionAttachmentDescriptor attachment;
        if (User.IsInRole(Roles.Admin))
            attachment = await assignments.GetAttachmentForAdminAsync(submissionId, ct);
        else if (User.IsInRole(Roles.Teacher))
            attachment = await assignments.GetAttachmentForTeacherAsync(submissionId, actor, ct);
        else if (User.IsInRole(Roles.Student))
            attachment = await assignments.GetAttachmentForStudentAsync(submissionId, actor, ct);
        else
            return Forbid();

        var format = Path.GetExtension(attachment.FileName);
        string downloadUrl;
        try
        {
            downloadUrl = mediaStorage.CreateTemporaryDownloadUrl(
                MediaAssetResourceType.Raw, attachment.PublicId, format, TimeSpan.FromMinutes(10));
        }
        catch (MediaAssetStorageNotConfiguredException)
        {
            return Problem(title: "Submission attachment storage is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        using var response = await httpClientFactory.CreateClient().GetAsync(
            downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Cloudinary attachment download returned HTTP {StatusCode} for submission {SubmissionId}.",
                (int)response.StatusCode, submissionId);
            return Problem(title: "The attachment is temporarily unavailable.", statusCode: StatusCodes.Status502BadGateway);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return File(bytes, attachment.ContentType, attachment.FileName);
    }
}
