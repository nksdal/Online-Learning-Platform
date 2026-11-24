using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class ProgressService(ApplicationDbContext db, TimeProvider clock) : IProgressService
{
    public async Task<CourseProgressResponse> GetCourseProgressAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        var course = await GetEnrolledCourseAsync(courseId, studentId, ct);
        var lessons = await db.Lessons.AsNoTracking().Where(lesson => lesson.CourseId == courseId)
            .OrderBy(lesson => lesson.Position).ThenBy(lesson => lesson.Title).ToListAsync(ct);
        var lessonIds = lessons.Select(lesson => lesson.Id).ToArray();
        var completed = await db.LessonProgress.AsNoTracking()
            .Where(progress => progress.StudentUserId == studentId && lessonIds.Contains(progress.LessonId))
            .ToDictionaryAsync(progress => progress.LessonId, progress => progress.CompletedAtUtc, ct);
        var items = lessons.Select(lesson =>
        {
            var isCompleted = completed.TryGetValue(lesson.Id, out var completedAtUtc);
            return new LessonProgressItem(lesson.Id, lesson.Title, lesson.Position,
                isCompleted, isCompleted ? completedAtUtc : null);
        }).ToArray();
        var completedCount = items.Count(item => item.IsCompleted);
        var percentage = lessons.Count == 0 ? 0 : decimal.Round(completedCount * 100m / lessons.Count, 1);

        return new CourseProgressResponse(
            course.Id,
            course.Title,
            course.Status.ToString(),
            lessons.Count,
            completedCount,
            percentage,
            items);
    }

    public async Task<IReadOnlyCollection<CourseProgressResponse>> GetMyProgressAsync(Guid studentId, CancellationToken ct)
    {
        var rows = await (
            from enrollment in db.Enrollments.AsNoTracking().Where(item => item.StudentUserId == studentId)
            join course in db.Courses.AsNoTracking() on enrollment.CourseId equals course.Id
            where course.Status == CourseStatus.Published || course.Status == CourseStatus.Archived
            join lesson in db.Lessons.AsNoTracking() on course.Id equals lesson.CourseId into courseLessons
            from lesson in courseLessons.DefaultIfEmpty()
            join progress in db.LessonProgress.AsNoTracking().Where(item => item.StudentUserId == studentId)
                on (lesson == null ? Guid.Empty : lesson.Id) equals progress.LessonId into lessonProgress
            from progress in lessonProgress.DefaultIfEmpty()
            select new
            {
                CourseId = course.Id,
                CourseTitle = course.Title,
                CourseStatus = course.Status,
                enrollment.EnrolledAtUtc,
                LessonId = lesson == null ? (Guid?)null : lesson.Id,
                LessonTitle = lesson == null ? null : lesson.Title,
                LessonPosition = lesson == null ? (int?)null : lesson.Position,
                CompletedAtUtc = progress == null ? (DateTime?)null : progress.CompletedAtUtc,
            }).ToListAsync(ct);

        return rows.GroupBy(row => row.CourseId)
            .OrderByDescending(group => group.First().EnrolledAtUtc)
            .Select(group =>
            {
                var items = group.Where(row => row.LessonId.HasValue)
                    .Select(row => new LessonProgressItem(
                        row.LessonId!.Value,
                        row.LessonTitle!,
                        row.LessonPosition!.Value,
                        row.CompletedAtUtc.HasValue,
                        row.CompletedAtUtc))
                    .OrderBy(item => item.Position)
                    .ThenBy(item => item.Title)
                    .ToArray();
                var completedCount = items.Count(item => item.IsCompleted);
                var percentage = items.Length == 0 ? 0 : decimal.Round(completedCount * 100m / items.Length, 1);
                var first = group.First();
                return new CourseProgressResponse(
                    first.CourseId,
                    first.CourseTitle,
                    first.CourseStatus.ToString(),
                    items.Length,
                    completedCount,
                    percentage,
                    items);
            })
            .ToArray();
    }

    public async Task<LessonCompletionResponse> CompleteLessonAsync(Guid lessonId, Guid studentId, CancellationToken ct)
    {
        var lesson = await db.Lessons.SingleOrDefaultAsync(item => item.Id == lessonId, ct)
            ?? throw new NotFoundException("Lesson was not found.");
        var course = await GetEnrolledCourseAsync(lesson.CourseId, studentId, ct);
        if (course.Status != CourseStatus.Published)
            throw new BusinessRuleException("Progress cannot be changed after a course is archived.");

        var progress = await db.LessonProgress.SingleOrDefaultAsync(
            item => item.LessonId == lessonId && item.StudentUserId == studentId, ct);
        if (progress is null)
        {
            progress = LessonProgress.Create(lessonId, studentId, clock.GetUtcNow().UtcDateTime);
            db.LessonProgress.Add(progress);
            await db.SaveChangesAsync(ct);
        }

        return new LessonCompletionResponse(lesson.Id, lesson.CourseId, true, progress.CompletedAtUtc);
    }

    public async Task DeleteLessonCompletionAsync(Guid lessonId, Guid studentId, CancellationToken ct)
    {
        var lesson = await db.Lessons.SingleOrDefaultAsync(item => item.Id == lessonId, ct)
            ?? throw new NotFoundException("Lesson was not found.");
        var course = await GetEnrolledCourseAsync(lesson.CourseId, studentId, ct);
        if (course.Status != CourseStatus.Published)
            throw new BusinessRuleException("Progress cannot be changed after a course is archived.");

        var progress = await db.LessonProgress.SingleOrDefaultAsync(
            item => item.LessonId == lessonId && item.StudentUserId == studentId, ct);
        if (progress is not null)
        {
            db.LessonProgress.Remove(progress);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<Course> GetEnrolledCourseAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == courseId && (item.Status == CourseStatus.Published || item.Status == CourseStatus.Archived), ct);
        if (course is null || !await db.Enrollments.AnyAsync(
                enrollment => enrollment.CourseId == courseId && enrollment.StudentUserId == studentId, ct))
            throw new NotFoundException("Course was not found.");
        return course;
    }
}
