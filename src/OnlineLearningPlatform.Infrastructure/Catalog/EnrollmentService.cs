using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Domain.Auditing;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class EnrollmentService(ApplicationDbContext db, TimeProvider clock) : IEnrollmentService
{
    public async Task<EnrollmentResponse> EnrollAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(item => item.Id == courseId, ct)
            ?? throw new NotFoundException("Course was not found.");
        if (course.Status != CourseStatus.Published)
            throw new BusinessRuleException("Only published courses can be enrolled in.");
        if (await db.Enrollments.AnyAsync(item => item.CourseId == courseId && item.StudentUserId == studentId, ct))
            throw new ConflictException("You are already enrolled in this course.");

        var enrollment = Enrollment.Create(courseId, studentId, clock.GetUtcNow().UtcDateTime);
        db.Enrollments.Add(enrollment);
        db.AuditLogs.Add(AuditLog.Create(clock.GetUtcNow().UtcDateTime, "StudentEnrolled", nameof(Enrollment),
            studentId, enrollment.Id.ToString(), $"Student enrolled in course '{course.Title}'."));
        await db.SaveChangesAsync(ct);
        return ToResponse(enrollment, course);
    }

    public async Task<IReadOnlyCollection<EnrollmentResponse>> ListMineAsync(Guid studentId, CancellationToken ct)
    {
        var rows = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.StudentUserId == studentId)
            .Join(db.Courses.AsNoTracking(), enrollment => enrollment.CourseId, course => course.Id,
                (enrollment, course) => new { Enrollment = enrollment, Course = course })
            .OrderByDescending(row => row.Enrollment.EnrolledAtUtc)
            .ToListAsync(ct);
        return rows.Select(row => ToResponse(row.Enrollment, row.Course)).ToArray();
    }

    private static EnrollmentResponse ToResponse(Enrollment enrollment, Course course) => new(
        enrollment.Id, course.Id, course.Title, course.Summary, course.ImageUrl, course.Status.ToString(), enrollment.EnrolledAtUtc);
}
