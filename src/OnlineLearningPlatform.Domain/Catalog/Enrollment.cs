using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public sealed class Enrollment : IAuditableEntity
{
    private Enrollment() { }

    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public DateTime EnrolledAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static Enrollment Create(Guid courseId, Guid studentUserId, DateTime enrolledAtUtc)
    {
        if (courseId == Guid.Empty) throw new ArgumentException("Course is required.", nameof(courseId));
        if (studentUserId == Guid.Empty) throw new ArgumentException("Student is required.", nameof(studentUserId));
        Guard.EnsureUtc(enrolledAtUtc, nameof(enrolledAtUtc));
        return new Enrollment
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            StudentUserId = studentUserId,
            EnrolledAtUtc = enrolledAtUtc,
        };
    }
}
