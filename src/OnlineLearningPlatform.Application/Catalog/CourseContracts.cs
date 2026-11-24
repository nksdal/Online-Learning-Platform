using FluentValidation;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Domain.Catalog;

namespace OnlineLearningPlatform.Application.Catalog;

public sealed record CourseWriteRequest(
    Guid CategoryId,
    string Title,
    string Summary,
    string Description,
    string? ImageUrl = null,
    string Duration = "Self-paced",
    decimal Price = 0m,
    string Difficulty = "Beginner",
    string? Prerequisites = null);
public sealed record CourseRejectionRequest(string Reason);
public sealed record CourseResponse(
    Guid Id, Guid CategoryId, Guid InstructorUserId, string Title, string Summary, string Description,
    string? ImageUrl, string Status, string? RejectionReason, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    string Duration, decimal Price, string Difficulty, string? Prerequisites);

/// <summary>Form-bound course fields used by administrators when editing any course status.</summary>
public sealed class AdminCourseUpdateRequest
{
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = "Self-paced";
    public decimal Price { get; set; }
    public string Difficulty { get; set; } = "Beginner";
    public string? Prerequisites { get; set; }
}

public sealed class CourseWriteRequestValidator : AbstractValidator<CourseWriteRequest>
{
    public CourseWriteRequestValidator()
    {
        RuleFor(request => request.CategoryId).NotEmpty();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(150);
        RuleFor(request => request.Summary).NotEmpty().MaximumLength(300);
        RuleFor(request => request.Description).NotEmpty().MaximumLength(10_000);
        RuleFor(request => request.ImageUrl)
            .Must(IsAllowedImageUrl)
            .WithMessage("ImageUrl must be an HTTPS URL or a local /images/courses path.");
        RuleFor(request => request.Duration).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Price).InclusiveBetween(0m, 99_999_999.99m)
            .Must(price => decimal.Round(price, 2) == price)
            .WithMessage("Price must be non-negative with at most two decimal places.");
        RuleFor(request => request.Difficulty).Must(IsAllowedDifficulty)
            .WithMessage("Difficulty must be Beginner, Intermediate, or Advanced.");
        RuleFor(request => request.Prerequisites).MaximumLength(2_000);
    }

    private static bool IsAllowedDifficulty(string value) =>
        Enum.TryParse<CourseDifficulty>(value, ignoreCase: false, out var difficulty) && Enum.IsDefined(difficulty);

    private static bool IsAllowedImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return true;
        var value = imageUrl.Trim();
        if (value.StartsWith("/images/courses/", StringComparison.Ordinal) &&
            !value.Contains("..", StringComparison.Ordinal) &&
            !value.Contains('\\')) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
    }
}

public sealed class CourseRejectionRequestValidator : AbstractValidator<CourseRejectionRequest>
{
    public CourseRejectionRequestValidator() => RuleFor(request => request.Reason).NotEmpty().MaximumLength(1_000);
}

public interface ICourseService
{
    Task<IReadOnlyCollection<CourseResponse>> ListAllAsync(CancellationToken cancellationToken);
    Task<CourseResponse> GetAnyAsync(Guid courseId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CourseResponse>> ListPublishedAsync(Guid? categoryId, CancellationToken cancellationToken);
    Task<CourseResponse> GetPublishedAsync(Guid courseId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CourseResponse>> ListInstructorAsync(Guid instructorId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CourseResponse>> ListPendingAsync(CancellationToken cancellationToken);
    Task<CourseResponse> CreateAsync(Guid instructorId, CourseWriteRequest request, CancellationToken cancellationToken);
    Task<CourseResponse> UpdateAsync(Guid instructorId, Guid courseId, CourseWriteRequest request, CancellationToken cancellationToken);
    Task<CourseResponse> UpdateByAdminAsync(
        Guid adminId,
        Guid courseId,
        AdminCourseUpdateRequest request,
        StoredCourseImage? uploadedImage,
        CancellationToken cancellationToken);
    Task<CourseResponse> SubmitAsync(Guid instructorId, Guid courseId, CancellationToken cancellationToken);
    Task<CourseResponse> ArchiveAsync(Guid instructorId, Guid courseId, CancellationToken cancellationToken);
    Task<CourseResponse> ApproveAsync(Guid adminId, Guid courseId, CancellationToken cancellationToken);
    Task<CourseResponse> RejectAsync(Guid adminId, Guid courseId, string reason, CancellationToken cancellationToken);
}
