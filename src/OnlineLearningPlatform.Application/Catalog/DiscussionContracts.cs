using FluentValidation;

namespace OnlineLearningPlatform.Application.Catalog;

public sealed record DiscussionPostRequest(string Body);
public sealed record ModerateDiscussionRequest(bool IsHidden, string? Reason);
public sealed record DiscussionPostResponse(
    Guid Id, Guid CourseId, Guid AuthorUserId, Guid? ParentPostId, string Body, bool IsHidden,
    string? ModerationReason, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed class DiscussionPostRequestValidator : AbstractValidator<DiscussionPostRequest>
{
    public DiscussionPostRequestValidator() => RuleFor(request => request.Body).NotEmpty().MaximumLength(5_000);
}

public sealed class ModerateDiscussionRequestValidator : AbstractValidator<ModerateDiscussionRequest>
{
    public ModerateDiscussionRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500).When(request => request.IsHidden);
        RuleFor(request => request.Reason).MaximumLength(500);
    }
}

public interface IDiscussionService
{
    Task<IReadOnlyCollection<DiscussionPostResponse>> ListForStudentAsync(Guid courseId, Guid studentId, CancellationToken ct);
    Task<IReadOnlyCollection<DiscussionPostResponse>> ListForTeacherAsync(Guid courseId, Guid teacherId, CancellationToken ct);
    Task<IReadOnlyCollection<DiscussionPostResponse>> ListForAdminAsync(Guid courseId, CancellationToken ct);
    Task<DiscussionPostResponse> CreateAsync(Guid courseId, Guid studentId, DiscussionPostRequest request, CancellationToken ct);
    Task<DiscussionPostResponse> ReplyAsync(Guid parentPostId, Guid studentId, DiscussionPostRequest request, CancellationToken ct);
    Task<DiscussionPostResponse> ModerateAsync(Guid postId, Guid actorId, bool isAdmin, ModerateDiscussionRequest request, CancellationToken ct);
}
