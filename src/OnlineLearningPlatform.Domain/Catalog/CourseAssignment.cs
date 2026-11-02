using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public sealed class CourseAssignment : IAuditableEntity
{
    public const int TitleMaxLength = 150;
    public const int InstructionsMaxLength = 10_000;
    public const int DefaultMaxAttempts = 1;

    private CourseAssignment() { }

    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Instructions { get; private set; } = string.Empty;
    public DateTime? DueAtUtc { get; private set; }
    public int MaxAttempts { get; private set; }
    public decimal MaxScore { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static CourseAssignment Create(
        Guid courseId, string title, string instructions, DateTime? dueAtUtc, int maxAttempts, decimal maxScore)
    {
        if (courseId == Guid.Empty) throw new ArgumentException("Course is required.", nameof(courseId));
        var assignment = new CourseAssignment { Id = Guid.NewGuid(), CourseId = courseId };
        assignment.Update(title, instructions, dueAtUtc, maxAttempts, maxScore);
        return assignment;
    }

    public void Update(string title, string instructions, DateTime? dueAtUtc, int maxAttempts, decimal maxScore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        if (maxAttempts is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        if (maxScore <= 0 || maxScore > 999_999.99m || decimal.Round(maxScore, 2) != maxScore)
            throw new ArgumentOutOfRangeException(nameof(maxScore), "Maximum score must be positive with at most two decimal places.");
        if (dueAtUtc.HasValue) Guard.EnsureUtc(dueAtUtc.Value, nameof(dueAtUtc));
        Title = title.Trim();
        Instructions = instructions.Trim();
        Guard.EnsureMaxLength(Title, TitleMaxLength, nameof(title));
        Guard.EnsureMaxLength(Instructions, InstructionsMaxLength, nameof(instructions));
        DueAtUtc = dueAtUtc;
        MaxAttempts = maxAttempts;
        MaxScore = maxScore;
    }
}
