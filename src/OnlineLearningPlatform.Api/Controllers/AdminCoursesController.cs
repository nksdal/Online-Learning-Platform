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
[Route("api/admin/courses")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminCoursesController(
    ICourseService courses,
    ILessonService lessons,
    IAssignmentService assignments,
    IDiscussionService discussions,
    ICourseImageStorage courseImages,
    ILogger<AdminCoursesController> logger) : ControllerBase
{
    private const long MaxCourseImageBytes = 8 * 1024 * 1024;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<CourseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAll(CancellationToken ct) => Ok(await courses.ListAllAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CourseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await courses.GetAnyAsync(id, ct));

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(9 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 9 * 1024 * 1024)]
    [ProducesResponseType(typeof(CourseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromForm] AdminCourseUpdateForm form,
        CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();

        var request = new AdminCourseUpdateRequest
        {
            CategoryId = form.CategoryId,
            Title = form.Title,
            Summary = form.Summary,
            Description = form.Description,
            Duration = form.Duration,
            Price = form.Price,
            Difficulty = form.Difficulty,
            Prerequisites = form.Prerequisites,
        };
        var image = form.Image;
        StoredCourseImage? uploadedImage = null;
        if (image is not null)
        {
            if (image.Length == 0)
                return Problem(title: "Choose a non-empty course image.", statusCode: StatusCodes.Status400BadRequest);
            if (image.Length > MaxCourseImageBytes)
                return Problem(title: "Course images must be 8 MB or smaller.", statusCode: StatusCodes.Status413PayloadTooLarge);

            await using var imageStream = image.OpenReadStream();
            var header = new byte[12];
            var bytesRead = 0;
            while (bytesRead < header.Length)
            {
                var read = await imageStream.ReadAsync(header.AsMemory(bytesRead), ct);
                if (read == 0) break;
                bytesRead += read;
            }
            imageStream.Position = 0;
            if (!IsSupportedImage(header, bytesRead))
                return Problem(title: "Upload a JPEG, PNG, or WebP image.", statusCode: StatusCodes.Status415UnsupportedMediaType);

            try
            {
                uploadedImage = await courseImages.UploadAsync(id, image.FileName, imageStream, ct);
            }
            catch (CourseImageStorageNotConfiguredException)
            {
                return Problem(
                    title: "Course image uploads are not configured. Set the Cloudinary credentials in the server environment.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                return Ok(await courses.UpdateByAdminAsync(
                    actor, id, request, uploadedImage, ct));
            }
            catch
            {
                await TryDeleteUploadedImageAsync(uploadedImage, id);
                throw;
            }
        }

        return Ok(await courses.UpdateByAdminAsync(actor, id, request, uploadedImage, ct));
    }

    [HttpGet("pending")]
    public async Task<IActionResult> ListPending(CancellationToken ct) => Ok(await courses.ListPendingAsync(ct));

    [HttpGet("{id:guid}/lessons")]
    public async Task<IActionResult> ListLessons(Guid id, CancellationToken ct) => Ok(await lessons.ListForAdminAsync(id, ct));

    [HttpGet("{id:guid}/assignments")]
    public async Task<IActionResult> ListAssignments(Guid id, CancellationToken ct) => Ok(await assignments.ListForAdminAsync(id, ct));

    [HttpGet("{id:guid}/discussions")]
    public async Task<IActionResult> ListDiscussions(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) => Ok(await discussions.ListForAdminAsync(id, page, pageSize, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await courses.ApproveAsync(actor, id, ct)) : Unauthorized();

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CourseRejectionRequest request, CancellationToken ct) =>
        TryActor(out var actor) ? Ok(await courses.RejectAsync(actor, id, request.Reason, ct)) : Unauthorized();

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out actor);

    private async Task TryDeleteUploadedImageAsync(StoredCourseImage image, Guid courseId)
    {
        try
        {
            await courseImages.DeleteAsync(image.PublicId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Could not clean up a newly uploaded Cloudinary image after course update failed for {CourseId}.", courseId);
        }
    }

    private static bool IsSupportedImage(byte[] header, int length)
    {
        var jpeg = length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var png = length >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        });
        var webp = length >= 12 &&
                   header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
                   header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';
        return jpeg || png || webp;
    }
}
