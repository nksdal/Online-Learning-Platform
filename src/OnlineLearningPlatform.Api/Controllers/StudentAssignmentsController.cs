using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Api.Models;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Student)]
public sealed class StudentAssignmentsController(IAssignmentService assignments) : ControllerBase
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    [HttpGet("/api/courses/{courseId:guid}/assignments")]
    public async Task<IActionResult> ListForCourse(Guid courseId, CancellationToken ct) =>
        TryActor(out var student) ? Ok(await assignments.ListForStudentAsync(courseId, student, ct)) : Unauthorized();

    [HttpPost("/api/assignments/{assignmentId:guid}/submissions")]
    [HttpPost("/api/assignments/{id:guid}/submit")]
    [ProducesResponseType(typeof(AssignmentSubmissionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Submit(
        Guid assignmentId, [FromRoute(Name = "id")] Guid? id, AssignmentSubmissionRequest request, CancellationToken ct)
    {
        if (!TryActor(out var student)) return Unauthorized();
        assignmentId = id ?? assignmentId;
        var response = await assignments.SubmitAsync(assignmentId, student, request, ct);
        return Created($"/api/assignments/{assignmentId}/submissions/me", response);
    }

    [HttpPost("/api/assignments/{assignmentId:guid}/submissions/with-attachment")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAttachmentBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAttachmentBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(AssignmentSubmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitWithAttachment(
        Guid assignmentId, [FromForm] AssignmentSubmissionUploadForm form, CancellationToken ct)
    {
        if (!TryActor(out var student)) return Unauthorized();
        var file = form.File;
        if (file is null || file.Length == 0)
            return Problem(title: "Choose a file to attach.", statusCode: StatusCodes.Status400BadRequest);
        if (file.Length > MaxAttachmentBytes)
            return Problem(title: "Submission attachments must be 10 MB or smaller.", statusCode: StatusCodes.Status413PayloadTooLarge);
        if (form.Text?.Length > 20_000)
            return Problem(title: "Submission text must be 20,000 characters or shorter.", statusCode: StatusCodes.Status400BadRequest);
        var safeFileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (safeFileName.Length > 255)
            return Problem(title: "Attachment file names must be 255 characters or shorter.", statusCode: StatusCodes.Status400BadRequest);

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
        stream.Position = 0;
        if (!IsSupportedAttachment(file.FileName, file.ContentType, header, read))
            return Problem(title: "Attach a PDF, Word, Excel, PowerPoint, TXT, CSV, or common image file.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);

        try
        {
            var response = await assignments.SubmitWithAttachmentAsync(
                assignmentId, student, new AssignmentSubmissionRequest(form.Text ?? string.Empty),
                safeFileName, file.ContentType, file.Length, stream, ct);
            return Created($"/api/assignments/{assignmentId}/submissions/me", response);
        }
        catch (OnlineLearningPlatform.Application.Abstractions.MediaAssetStorageNotConfiguredException)
        {
            return Problem(title: "Submission attachments are not configured. Set the Cloudinary credentials in the server environment.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("/api/assignments/{assignmentId:guid}/submissions/me")]
    public async Task<IActionResult> ListMine(Guid assignmentId, CancellationToken ct) =>
        TryActor(out var student) ? Ok(await assignments.ListMineAsync(assignmentId, student, ct)) : Unauthorized();

    [HttpGet("/api/courses/{courseId:guid}/submissions/me")]
    public async Task<IActionResult> ListMineForCourse(Guid courseId, CancellationToken ct) =>
        TryActor(out var student) ? Ok(await assignments.ListMineForCourseAsync(courseId, student, ct)) : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);

    private static bool IsSupportedAttachment(string fileName, string contentType, byte[] header, int length)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var mime = contentType.ToLowerInvariant();
        return extension switch
        {
            ".pdf" => mime == "application/pdf" && length >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            ".docx" => mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" && IsZip(header, length),
            ".xlsx" => mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" && IsZip(header, length),
            ".pptx" => mime == "application/vnd.openxmlformats-officedocument.presentationml.presentation" && IsZip(header, length),
            ".txt" => mime == "text/plain" && IsText(header, length),
            ".csv" => mime is "text/csv" or "application/vnd.ms-excel" && IsText(header, length),
            ".jpg" or ".jpeg" => mime == "image/jpeg" && length >= 3 &&
                header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => mime == "image/png" && length >= 8 && header.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => mime == "image/webp" && length >= 12 &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false,
        };
    }

    private static bool IsZip(byte[] header, int length) =>
        length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] is 0x03 or 0x05 or 0x07;

    private static bool IsText(byte[] header, int length) =>
        length > 0 && !header.AsSpan(0, length).Contains((byte)0);
}
