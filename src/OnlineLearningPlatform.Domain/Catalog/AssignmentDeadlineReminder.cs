using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public sealed class AssignmentDeadlineReminder : IAuditableEntity
{
    public const int MaxDeliveryAttempts = 5;
    public const int LastErrorMaxLength = 500;

    private AssignmentDeadlineReminder() { }

    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public static AssignmentDeadlineReminder Create(Guid assignmentId, Guid studentUserId)
    {
        if (assignmentId == Guid.Empty) throw new ArgumentException("Assignment is required.", nameof(assignmentId));
        if (studentUserId == Guid.Empty) throw new ArgumentException("Student is required.", nameof(studentUserId));
        return new AssignmentDeadlineReminder
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            StudentUserId = studentUserId,
        };
    }

    public void MarkSent(DateTime sentAtUtc)
    {
        Guard.EnsureUtc(sentAtUtc, nameof(sentAtUtc));
        AttemptCount++;
        SentAtUtc = sentAtUtc;
        LastError = null;
    }

    public void RecordFailure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        AttemptCount++;
        LastError = Guard.Truncate(error, LastErrorMaxLength);
    }
}
