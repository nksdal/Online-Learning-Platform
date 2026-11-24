using System.Data;
using Microsoft.Data.SqlClient;
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
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var course = await db.Courses.SingleOrDefaultAsync(item => item.Id == courseId, ct)
                    ?? throw new NotFoundException("Course was not found.");
                if (course.Status != CourseStatus.Published)
                    throw new BusinessRuleException("Only published courses can be enrolled in.");
                if (await db.Enrollments.AnyAsync(item => item.CourseId == courseId && item.StudentUserId == studentId, ct))
                    throw new ConflictException("You are already enrolled in this course.");

                var now = clock.GetUtcNow().UtcDateTime;
                var enrollment = Enrollment.Create(courseId, studentId, now);
                db.Enrollments.Add(enrollment);
                db.AuditLogs.Add(AuditLog.Create(now, "StudentEnrolled", nameof(Enrollment),
                    studentId, enrollment.Id.ToString(), $"Student enrolled in course '{course.Title}'."));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return ToResponse(enrollment, course);
            });
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("You are already enrolled in this course.");
        }
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

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 2601 or 2627 })
                return true;
        return false;
    }
}
