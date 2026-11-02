using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public sealed class CourseDiscussionPost : IAuditableEntity
{
    public const int BodyMaxLength = 5_000;
    public const int ModerationReasonMaxLength = 500;

    private CourseDiscussionPost() { }

    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid? ParentPostId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool IsHidden { get; private set; }
    public string? ModerationReason { get; private set; }
    public Guid? ModeratedByUserId { get; private set; }
    public DateTime? ModeratedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static CourseDiscussionPost Create(Guid courseId, Guid authorUserId, Guid? parentPostId, string body)
    {
        if (courseId == Guid.Empty) throw new ArgumentException("Course is required.", nameof(courseId));
        if (authorUserId == Guid.Empty) throw new ArgumentException("Author is required.", nameof(authorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var trimmedBody = body.Trim();
        Guard.EnsureMaxLength(trimmedBody, BodyMaxLength, nameof(body));
        return new CourseDiscussionPost
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            AuthorUserId = authorUserId,
            ParentPostId = parentPostId,
            Body = trimmedBody,
        };
    }

    public void SetModeration(bool isHidden, string? reason, Guid moderatorUserId, DateTime moderatedAtUtc)
    {
        if (moderatorUserId == Guid.Empty) throw new ArgumentException("Moderator is required.", nameof(moderatorUserId));
        Guard.EnsureUtc(moderatedAtUtc, nameof(moderatedAtUtc));
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (isHidden && trimmedReason is null) throw new ArgumentException("A reason is required when hiding a post.", nameof(reason));
        if (trimmedReason is not null) Guard.EnsureMaxLength(trimmedReason, ModerationReasonMaxLength, nameof(reason));
        IsHidden = isHidden;
        ModerationReason = isHidden ? trimmedReason : null;
        ModeratedByUserId = moderatorUserId;
        ModeratedAtUtc = moderatedAtUtc;
    }
}
