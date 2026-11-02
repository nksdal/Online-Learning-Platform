using FluentValidation;

namespace OnlineLearningPlatform.Application.Catalog;

public sealed record LessonWriteRequest(string Title, string Content, int Position);
public sealed record LessonResponse(
    Guid Id, Guid CourseId, string Title, string Content, int Position, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record EnrollmentResponse(
    Guid Id, Guid CourseId, string CourseTitle, string CourseSummary, string CourseStatus, DateTime EnrolledAtUtc);
public sealed record LessonProgressItem(Guid LessonId, string Title, int Position, bool IsCompleted, DateTime? CompletedAtUtc);
public sealed record CourseProgressResponse(
    Guid CourseId, string CourseTitle, string CourseStatus, int TotalLessons, int CompletedLessons,
    decimal CompletionPercentage, IReadOnlyCollection<LessonProgressItem> Lessons);
public sealed record LessonCompletionResponse(Guid LessonId, Guid CourseId, bool IsCompleted, DateTime? CompletedAtUtc);

public sealed class LessonWriteRequestValidator : AbstractValidator<LessonWriteRequest>
{
    public LessonWriteRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(150);
        RuleFor(request => request.Content).NotEmpty().MaximumLength(20_000);
        RuleFor(request => request.Position).GreaterThan(0);
    }
}

public interface ILessonService
{
    Task<IReadOnlyCollection<LessonResponse>> ListForStudentAsync(Guid courseId, Guid studentId, CancellationToken ct);
    Task<IReadOnlyCollection<LessonResponse>> ListForTeacherAsync(Guid courseId, Guid teacherId, CancellationToken ct);
    Task<IReadOnlyCollection<LessonResponse>> ListForAdminAsync(Guid courseId, CancellationToken ct);
    Task<LessonResponse> CreateAsync(Guid courseId, Guid teacherId, LessonWriteRequest request, CancellationToken ct);
    Task<LessonResponse> UpdateAsync(Guid lessonId, Guid teacherId, LessonWriteRequest request, CancellationToken ct);
    Task DeleteAsync(Guid lessonId, Guid teacherId, CancellationToken ct);
}

public interface IEnrollmentService
{
    Task<EnrollmentResponse> EnrollAsync(Guid courseId, Guid studentId, CancellationToken ct);
    Task<IReadOnlyCollection<EnrollmentResponse>> ListMineAsync(Guid studentId, CancellationToken ct);
}

public interface IProgressService
{
    Task<CourseProgressResponse> GetCourseProgressAsync(Guid courseId, Guid studentId, CancellationToken ct);
    Task<LessonCompletionResponse> CompleteLessonAsync(Guid lessonId, Guid studentId, CancellationToken ct);
    Task DeleteLessonCompletionAsync(Guid lessonId, Guid studentId, CancellationToken ct);
}
