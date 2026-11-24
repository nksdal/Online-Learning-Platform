using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public sealed class AssignmentSubmission : IAuditableEntity
{
    public const int TextMaxLength = 20_000;
    public const int FeedbackMaxLength = 2_000;
    public const int AttachmentUrlMaxLength = 2_048;
    public const int AttachmentPublicIdMaxLength = 255;
    public const int AttachmentFileNameMaxLength = 255;
    public const int AttachmentContentTypeMaxLength = 128;

    private AssignmentSubmission() { }

    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public int AttemptNumber { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? AttachmentUrl { get; private set; }
    public string? AttachmentPublicId { get; private set; }
    public string? AttachmentFileName { get; private set; }
    public string? AttachmentContentType { get; private set; }
    public long? AttachmentSizeBytes { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public decimal? Score { get; private set; }
    public string? Feedback { get; private set; }
    public DateTime? GradedAtUtc { get; private set; }
    public Guid? GradedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static AssignmentSubmission Create(
        Guid assignmentId, Guid studentUserId, int attemptNumber, string text, DateTime submittedAtUtc,
        string? attachmentUrl = null, string? attachmentPublicId = null,
        string? attachmentFileName = null, string? attachmentContentType = null, long? attachmentSizeBytes = null)
    {
        if (assignmentId == Guid.Empty) throw new ArgumentException("Assignment is required.", nameof(assignmentId));
        if (studentUserId == Guid.Empty) throw new ArgumentException("Student is required.", nameof(studentUserId));
        if (attemptNumber < 1) throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        Guard.EnsureUtc(submittedAtUtc, nameof(submittedAtUtc));
        var trimmedText = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedText) && string.IsNullOrWhiteSpace(attachmentPublicId))
            throw new ArgumentException("A written response or file attachment is required.", nameof(text));
        Guard.EnsureMaxLength(trimmedText, TextMaxLength, nameof(text));
        if (!string.IsNullOrWhiteSpace(attachmentPublicId))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(attachmentUrl);
            ArgumentException.ThrowIfNullOrWhiteSpace(attachmentFileName);
            ArgumentException.ThrowIfNullOrWhiteSpace(attachmentContentType);
            if (!Uri.TryCreate(attachmentUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException("Attachment URL must be a valid HTTPS URL.", nameof(attachmentUrl));
            if (attachmentSizeBytes is null or <= 0)
                throw new ArgumentOutOfRangeException(nameof(attachmentSizeBytes));
            Guard.EnsureMaxLength(attachmentPublicId, AttachmentPublicIdMaxLength, nameof(attachmentPublicId));
            Guard.EnsureMaxLength(attachmentFileName, AttachmentFileNameMaxLength, nameof(attachmentFileName));
            Guard.EnsureMaxLength(attachmentContentType, AttachmentContentTypeMaxLength, nameof(attachmentContentType));
        }
        return new AssignmentSubmission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            StudentUserId = studentUserId,
            AttemptNumber = attemptNumber,
            Text = trimmedText,
            AttachmentUrl = attachmentUrl,
            AttachmentPublicId = attachmentPublicId,
            AttachmentFileName = attachmentFileName,
            AttachmentContentType = attachmentContentType,
            AttachmentSizeBytes = attachmentSizeBytes,
            SubmittedAtUtc = submittedAtUtc,
        };
    }

    public void Grade(decimal score, string? feedback, Guid graderUserId, DateTime gradedAtUtc, decimal maxScore)
    {
        if (graderUserId == Guid.Empty) throw new ArgumentException("Grader is required.", nameof(graderUserId));
        if (maxScore <= 0 || maxScore > CourseAssignment.MaxScoreLimit ||
            score < 0 || score > maxScore || score > CourseAssignment.MaxScoreLimit ||
            decimal.Round(score, 2) != score)
            throw new ArgumentOutOfRangeException(nameof(score), $"Score must be between 0 and {maxScore}, with at most two decimal places (maximum scale 10.0).");
        Guard.EnsureUtc(gradedAtUtc, nameof(gradedAtUtc));
        var trimmedFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        if (trimmedFeedback is not null) Guard.EnsureMaxLength(trimmedFeedback, FeedbackMaxLength, nameof(feedback));
        Score = score;
        Feedback = trimmedFeedback;
        GradedAtUtc = gradedAtUtc;
        GradedByUserId = graderUserId;
    }
}
