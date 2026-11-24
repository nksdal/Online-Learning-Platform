using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

/// <summary>A student's completed lesson. No row means the lesson is not complete.</summary>
public sealed class LessonProgress : IAuditableEntity
{
    private LessonProgress() { }

    public Guid Id { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public DateTime CompletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static LessonProgress Create(Guid lessonId, Guid studentUserId, DateTime completedAtUtc)
    {
        if (lessonId == Guid.Empty) throw new ArgumentException("Lesson is required.", nameof(lessonId));
        if (studentUserId == Guid.Empty) throw new ArgumentException("Student is required.", nameof(studentUserId));
        Guard.EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        return new LessonProgress
        {
            Id = Guid.NewGuid(),
            LessonId = lessonId,
            StudentUserId = studentUserId,
            CompletedAtUtc = completedAtUtc,
        };
    }
}
