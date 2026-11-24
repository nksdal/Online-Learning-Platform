using Microsoft.AspNetCore.Http;

namespace OnlineLearningPlatform.Api.Models;

public sealed class LessonVideoUploadForm
{
    public IFormFile? File { get; set; }
}

public sealed class AssignmentSubmissionUploadForm
{
    public string? Text { get; set; }
    public IFormFile? File { get; set; }
}
