using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Domain.Auditing;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class LessonService(ApplicationDbContext db, TimeProvider clock) : ILessonService
{
    public async Task<IReadOnlyCollection<LessonResponse>> ListForStudentAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == courseId && (item.Status == CourseStatus.Published || item.Status == CourseStatus.Archived), ct);
        if (course is null || !await db.Enrollments.AnyAsync(
                enrollment => enrollment.CourseId == courseId && enrollment.StudentUserId == studentId, ct))
            throw new NotFoundException("Course was not found.");

        return await ListAsync(courseId, ct);
    }

    public async Task<IReadOnlyCollection<LessonResponse>> ListForTeacherAsync(Guid courseId, Guid teacherId, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(course => course.Id == courseId && course.InstructorUserId == teacherId, ct))
            throw new NotFoundException("Course was not found.");
        return await ListAsync(courseId, ct);
    }

    public async Task<IReadOnlyCollection<LessonResponse>> ListForAdminAsync(Guid courseId, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(course => course.Id == courseId, ct))
            throw new NotFoundException("Course was not found.");
        return await ListAsync(courseId, ct);
    }

    public async Task<LessonResponse> CreateAsync(Guid courseId, Guid teacherId, LessonWriteRequest request, CancellationToken ct)
    {
        var course = await GetEditableCourseAsync(courseId, teacherId, ct);
        var lesson = Lesson.Create(course.Id, request.Title, request.Content, request.Position);
        db.Lessons.Add(lesson);
        AddAudit(teacherId, "LessonCreated", lesson);
        await db.SaveChangesAsync(ct);
        return ToResponse(lesson);
    }

    public async Task<LessonResponse> UpdateAsync(Guid lessonId, Guid teacherId, LessonWriteRequest request, CancellationToken ct)
    {
        var lesson = await db.Lessons.SingleOrDefaultAsync(item => item.Id == lessonId, ct)
            ?? throw new NotFoundException("Lesson was not found.");
        await GetEditableCourseAsync(lesson.CourseId, teacherId, ct);
        lesson.Update(request.Title, request.Content, request.Position);
        AddAudit(teacherId, "LessonUpdated", lesson);
        await db.SaveChangesAsync(ct);
        return ToResponse(lesson);
    }

    public async Task DeleteAsync(Guid lessonId, Guid teacherId, CancellationToken ct)
    {
        var lesson = await db.Lessons.SingleOrDefaultAsync(item => item.Id == lessonId, ct)
            ?? throw new NotFoundException("Lesson was not found.");
        await GetEditableCourseAsync(lesson.CourseId, teacherId, ct);
        AddAudit(teacherId, "LessonDeleted", lesson);
        db.Lessons.Remove(lesson);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Course> GetEditableCourseAsync(Guid courseId, Guid teacherId, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(
            item => item.Id == courseId && item.InstructorUserId == teacherId, ct)
            ?? throw new NotFoundException("Course was not found.");
        if (course.Status is CourseStatus.PendingApproval or CourseStatus.Archived)
            throw new BusinessRuleException("Lessons cannot be changed while the course is pending review or archived.");
        return course;
    }

    private async Task<IReadOnlyCollection<LessonResponse>> ListAsync(Guid courseId, CancellationToken ct) =>
        (await db.Lessons.AsNoTracking().Where(lesson => lesson.CourseId == courseId)
            .OrderBy(lesson => lesson.Position).ThenBy(lesson => lesson.Title).ToListAsync(ct))
            .Select(ToResponse).ToArray();

    private void AddAudit(Guid actorId, string action, Lesson lesson) => db.AuditLogs.Add(AuditLog.Create(
        clock.GetUtcNow().UtcDateTime, action, nameof(Lesson), actorId, lesson.Id.ToString(),
        $"Lesson '{lesson.Title}' in course {lesson.CourseId} was changed."));

    private static LessonResponse ToResponse(Lesson lesson) => new(
        lesson.Id, lesson.CourseId, lesson.Title, lesson.Content, lesson.Position, lesson.CreatedAtUtc, lesson.UpdatedAtUtc);
}
