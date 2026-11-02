using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public sealed class AssignmentSubmission : IAuditableEntity
{
    public const int TextMaxLength = 20_000;
    public const int FeedbackMaxLength = 2_000;

    private AssignmentSubmission() { }

    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public int AttemptNumber { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; private set; }
    public decimal? Score { get; private set; }
    public string? Feedback { get; private set; }
    public DateTime? GradedAtUtc { get; private set; }
    public Guid? GradedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static AssignmentSubmission Create(
        Guid assignmentId, Guid studentUserId, int attemptNumber, string text, DateTime submittedAtUtc)
    {
        if (assignmentId == Guid.Empty) throw new ArgumentException("Assignment is required.", nameof(assignmentId));
        if (studentUserId == Guid.Empty) throw new ArgumentException("Student is required.", nameof(studentUserId));
        if (attemptNumber < 1) throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Guard.EnsureUtc(submittedAtUtc, nameof(submittedAtUtc));
        var trimmedText = text.Trim();
        Guard.EnsureMaxLength(trimmedText, TextMaxLength, nameof(text));
        return new AssignmentSubmission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            StudentUserId = studentUserId,
            AttemptNumber = attemptNumber,
            Text = trimmedText,
            SubmittedAtUtc = submittedAtUtc,
        };
    }

    public void Grade(decimal score, string? feedback, Guid graderUserId, DateTime gradedAtUtc, decimal maxScore)
    {
        if (graderUserId == Guid.Empty) throw new ArgumentException("Grader is required.", nameof(graderUserId));
        if (score < 0 || score > maxScore || decimal.Round(score, 2) != score)
            throw new ArgumentOutOfRangeException(nameof(score), $"Score must be between 0 and {maxScore}, with at most two decimal places.");
        Guard.EnsureUtc(gradedAtUtc, nameof(gradedAtUtc));
        var trimmedFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        if (trimmedFeedback is not null) Guard.EnsureMaxLength(trimmedFeedback, FeedbackMaxLength, nameof(feedback));
        Score = score;
        Feedback = trimmedFeedback;
        GradedAtUtc = gradedAtUtc;
        GradedByUserId = graderUserId;
    }
}
