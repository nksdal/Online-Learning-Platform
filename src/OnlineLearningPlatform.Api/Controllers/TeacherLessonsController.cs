using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Api.Models;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Catalog;

namespace OnlineLearningPlatform.Api.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize(Policy = AuthorizationPolicies.Teacher)]
public sealed class TeacherLessonsController(ILessonService lessons) : ControllerBase
{
    private const long MaxVideoBytes = 100 * 1024 * 1024;

    [HttpGet("courses/{courseId:guid}/lessons")]
    public async Task<IActionResult> List(Guid courseId, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await lessons.ListForTeacherAsync(courseId, actor, ct)) : Unauthorized();

    [HttpPost("courses/{courseId:guid}/lessons")]
    [HttpPost("/api/courses/{courseId:guid}/lessons")]
    [ProducesResponseType(typeof(LessonResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid courseId, LessonWriteRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        var lesson = await lessons.CreateAsync(courseId, actor, request, ct);
        return Created($"/api/teacher/courses/{courseId}/lessons", lesson);
    }

    [HttpPut("lessons/{lessonId:guid}")]
    public async Task<IActionResult> Update(Guid lessonId, LessonWriteRequest request, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await lessons.UpdateAsync(lessonId, actor, request, ct)) : Unauthorized();

    [HttpPut("/api/courses/{courseId:guid}/lessons/{lessonId:guid}")]
    public async Task<IActionResult> UpdateInCourse(
        Guid courseId, Guid lessonId, LessonWriteRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        var courseLessons = await lessons.ListForTeacherAsync(courseId, actor, ct);
        if (!courseLessons.Any(item => item.Id == lessonId)) return NotFound();
        return Ok(await lessons.UpdateAsync(lessonId, actor, request, ct));
    }

    [HttpPut("lessons/{lessonId:guid}/video")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxVideoBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxVideoBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(LessonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadVideo(Guid lessonId, [FromForm] LessonVideoUploadForm form, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        var file = form.File;
        if (file is null || file.Length == 0)
            return Problem(title: "Choose a video file to upload.", statusCode: StatusCodes.Status400BadRequest);
        if (file.Length > MaxVideoBytes)
            return Problem(title: "Lesson videos must be 100 MB or smaller.", statusCode: StatusCodes.Status413PayloadTooLarge);
        var safeFileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (safeFileName.Length > 255)
            return Problem(title: "Video file names must be 255 characters or shorter.", statusCode: StatusCodes.Status400BadRequest);

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
        stream.Position = 0;
        if (!IsSupportedVideo(safeFileName, file.ContentType, header, read))
            return Problem(title: "Upload an MP4 or WebM video.", statusCode: StatusCodes.Status415UnsupportedMediaType);

        try
        {
            var lesson = await lessons.UploadVideoAsync(lessonId, actor, safeFileName, file.ContentType, stream, ct);
            return Ok(lesson);
        }
        catch (OnlineLearningPlatform.Application.Abstractions.MediaAssetStorageNotConfiguredException)
        {
            return Problem(title: "Video uploads are not configured. Set the Cloudinary credentials in the server environment.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpDelete("lessons/{lessonId:guid}/video")]
    public async Task<IActionResult> RemoveVideo(Guid lessonId, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        await lessons.RemoveVideoAsync(lessonId, actor, ct);
        return NoContent();
    }

    [HttpDelete("lessons/{lessonId:guid}")]
    public async Task<IActionResult> Delete(Guid lessonId, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        await lessons.DeleteAsync(lessonId, actor, ct);
        return NoContent();
    }

    [HttpDelete("/api/courses/{courseId:guid}/lessons/{lessonId:guid}")]
    public async Task<IActionResult> DeleteFromCourse(Guid courseId, Guid lessonId, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        var courseLessons = await lessons.ListForTeacherAsync(courseId, actor, ct);
        if (!courseLessons.Any(item => item.Id == lessonId)) return NotFound();
        await lessons.DeleteAsync(lessonId, actor, ct);
        return NoContent();
    }

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);

    private static bool IsSupportedVideo(string fileName, string contentType, byte[] header, int length)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) &&
            contentType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase))
            return length >= 8 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8);
        if (extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) &&
            contentType.Equals("video/webm", StringComparison.OrdinalIgnoreCase))
            return length >= 4 && header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 });
        return false;
    }
}
