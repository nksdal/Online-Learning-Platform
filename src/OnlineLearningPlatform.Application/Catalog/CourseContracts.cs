using FluentValidation;

namespace OnlineLearningPlatform.Application.Catalog;

public sealed record CourseWriteRequest(
    Guid CategoryId, string Title, string Summary, string Description, string? ImageUrl = null);
public sealed record CourseRejectionRequest(string Reason);
public sealed record CourseResponse(
    Guid Id, Guid CategoryId, Guid InstructorUserId, string Title, string Summary, string Description,
    string? ImageUrl, string Status, string? RejectionReason, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

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
    }

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
    Task<CourseResponse> SubmitAsync(Guid instructorId, Guid courseId, CancellationToken cancellationToken);
    Task<CourseResponse> ArchiveAsync(Guid instructorId, Guid courseId, CancellationToken cancellationToken);
    Task<CourseResponse> ApproveAsync(Guid adminId, Guid courseId, CancellationToken cancellationToken);
    Task<CourseResponse> RejectAsync(Guid adminId, Guid courseId, string reason, CancellationToken cancellationToken);
}
