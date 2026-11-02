using FluentValidation;

namespace OnlineLearningPlatform.Application.Catalog;

public sealed record AssignmentWriteRequest(
    string Title, string Instructions, DateTime? DueAtUtc, int MaxAttempts = 1, decimal MaxScore = 100);
public sealed record AssignmentSubmissionRequest(string Text);
public sealed record GradeSubmissionRequest(decimal Score, string? Feedback);
public sealed record AssignmentResponse(
    Guid Id, Guid CourseId, string Title, string Instructions, DateTime? DueAtUtc,
    int MaxAttempts, decimal MaxScore, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record AssignmentSubmissionResponse(
    Guid Id, Guid AssignmentId, Guid StudentUserId, int AttemptNumber, string Text, DateTime SubmittedAtUtc,
    decimal? Score, string? Feedback, DateTime? GradedAtUtc, Guid? GradedByUserId);

public sealed class AssignmentWriteRequestValidator : AbstractValidator<AssignmentWriteRequest>
{
    public AssignmentWriteRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(150);
        RuleFor(request => request.Instructions).NotEmpty().MaximumLength(10_000);
        RuleFor(request => request.MaxAttempts).InclusiveBetween(1, 20);
        RuleFor(request => request.MaxScore).InclusiveBetween(0.01m, 999_999.99m)
            .Must(score => decimal.Round(score, 2) == score);
        RuleFor(request => request.DueAtUtc)
            .Must(date => !date.HasValue || date.Value.Kind == DateTimeKind.Utc)
            .WithMessage("DueAtUtc must be supplied in UTC (with a Z suffix).");
    }
}

public sealed class AssignmentSubmissionRequestValidator : AbstractValidator<AssignmentSubmissionRequest>
{
    public AssignmentSubmissionRequestValidator() => RuleFor(request => request.Text).NotEmpty().MaximumLength(20_000);
}

public sealed class GradeSubmissionRequestValidator : AbstractValidator<GradeSubmissionRequest>
{
    public GradeSubmissionRequestValidator()
    {
        RuleFor(request => request.Score).GreaterThanOrEqualTo(0).Must(score => decimal.Round(score, 2) == score);
        RuleFor(request => request.Feedback).MaximumLength(2_000);
    }
}

public interface IAssignmentService
{
    Task<IReadOnlyCollection<AssignmentResponse>> ListForStudentAsync(Guid courseId, Guid studentId, CancellationToken ct);
    Task<IReadOnlyCollection<AssignmentResponse>> ListForTeacherAsync(Guid courseId, Guid teacherId, CancellationToken ct);
    Task<IReadOnlyCollection<AssignmentResponse>> ListForAdminAsync(Guid courseId, CancellationToken ct);
    Task<AssignmentResponse> CreateAsync(Guid courseId, Guid teacherId, AssignmentWriteRequest request, CancellationToken ct);
    Task<AssignmentResponse> UpdateAsync(Guid assignmentId, Guid teacherId, AssignmentWriteRequest request, CancellationToken ct);
    Task<AssignmentSubmissionResponse> SubmitAsync(Guid assignmentId, Guid studentId, AssignmentSubmissionRequest request, CancellationToken ct);
    Task<IReadOnlyCollection<AssignmentSubmissionResponse>> ListMineAsync(Guid assignmentId, Guid studentId, CancellationToken ct);
    Task<IReadOnlyCollection<AssignmentSubmissionResponse>> ListSubmissionsForTeacherAsync(Guid assignmentId, Guid teacherId, CancellationToken ct);
    Task<IReadOnlyCollection<AssignmentSubmissionResponse>> ListSubmissionsForAdminAsync(Guid assignmentId, CancellationToken ct);
    Task<AssignmentSubmissionResponse> GradeAsync(Guid submissionId, Guid teacherId, GradeSubmissionRequest request, CancellationToken ct);
}
