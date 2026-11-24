using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class AdminAnalyticsService(ApplicationDbContext db, TimeProvider clock) : IAdminAnalyticsService
{
    public async Task<AdminAnalyticsResponse> GetAsync(int days, CancellationToken cancellationToken)
    {
        if (days is not (7 or 30 or 90 or 365))
            throw new ArgumentOutOfRangeException(nameof(days), "Choose a 7, 30, 90, or 365 day reporting range.");

        var now = clock.GetUtcNow().UtcDateTime;
        var rangeStart = now.AddDays(-days);
        var trendStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        var users = db.Users.AsNoTracking();
        var totalUsers = await users.CountAsync(cancellationToken);
        var activeUsers = await users.CountAsync(user => user.IsActive && !user.IsDeleted, cancellationToken);
        var roleCounts = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join user in users on userRole.UserId equals user.Id
            where !user.IsDeleted
            group userRole by role.Name into roleGroup
            select new { Role = roleGroup.Key, Count = roleGroup.Select(row => row.UserId).Distinct().Count() }
        ).ToListAsync(cancellationToken);
        var students = roleCounts.SingleOrDefault(item => item.Role == Roles.Student)?.Count ?? 0;
        var teachers = roleCounts.SingleOrDefault(item => item.Role == Roles.Teacher)?.Count ?? 0;

        var courses = await db.Courses.AsNoTracking()
            .Select(course => new { course.Id, course.Title, course.Status })
            .ToListAsync(cancellationToken);
        var totalEnrollments = await db.Enrollments.CountAsync(cancellationToken);
        var lessonParticipationCount = await (
            from enrollment in db.Enrollments.AsNoTracking()
            join lesson in db.Lessons.AsNoTracking() on enrollment.CourseId equals lesson.CourseId
            select lesson.Id
        ).CountAsync(cancellationToken);
        var completedLessons = await db.LessonProgress.CountAsync(cancellationToken);
        var completionRate = lessonParticipationCount == 0
            ? 0m
            : Math.Round(completedLessons * 100m / lessonParticipationCount, 1);

        var submissionQuery = db.AssignmentSubmissions.AsNoTracking();
        var totalSubmissions = await submissionQuery.CountAsync(cancellationToken);
        var gradedSubmissions = await submissionQuery.CountAsync(item => item.Score != null, cancellationToken);
        var averageGrade = await submissionQuery.Where(item => item.Score != null)
            .Select(item => item.Score).AverageAsync(cancellationToken);

        var newUsersInRange = await users.CountAsync(item => item.CreatedAtUtc >= rangeStart && item.CreatedAtUtc < now, cancellationToken);
        var enrollmentsInRange = await db.Enrollments.CountAsync(
            item => item.EnrolledAtUtc >= rangeStart && item.EnrolledAtUtc < now, cancellationToken);
        var submissionsInRange = await submissionQuery.CountAsync(
            item => item.SubmittedAtUtc >= rangeStart && item.SubmittedAtUtc < now, cancellationToken);

        var userTrend = await users.Where(item => item.CreatedAtUtc >= trendStart && item.CreatedAtUtc < now)
            .GroupBy(item => new { item.CreatedAtUtc.Year, item.CreatedAtUtc.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var enrollmentTrend = await db.Enrollments.AsNoTracking()
            .Where(item => item.EnrolledAtUtc >= trendStart && item.EnrolledAtUtc < now)
            .GroupBy(item => new { item.EnrolledAtUtc.Year, item.EnrolledAtUtc.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var submissionTrend = await submissionQuery.Where(item => item.SubmittedAtUtc >= trendStart && item.SubmittedAtUtc < now)
            .GroupBy(item => new { item.SubmittedAtUtc.Year, item.SubmittedAtUtc.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var enrollmentCounts = await db.Enrollments.AsNoTracking()
            .GroupBy(item => item.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CourseId, item => item.Count, cancellationToken);
        var lessonCounts = await db.Lessons.AsNoTracking()
            .GroupBy(item => item.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CourseId, item => item.Count, cancellationToken);
        var completedByCourse = await (
            from progress in db.LessonProgress.AsNoTracking()
            join lesson in db.Lessons.AsNoTracking() on progress.LessonId equals lesson.Id
            group progress by lesson.CourseId into completionGroup
            select new { CourseId = completionGroup.Key, Count = completionGroup.Count() }
        ).ToDictionaryAsync(item => item.CourseId, item => item.Count, cancellationToken);

        var topCourses = courses
            .Select(course =>
            {
                var enrollments = enrollmentCounts.GetValueOrDefault(course.Id);
                var lessonCount = lessonCounts.GetValueOrDefault(course.Id);
                var possibleCompletions = enrollments * lessonCount;
                var completionCount = completedByCourse.GetValueOrDefault(course.Id);
                var rate = possibleCompletions == 0 ? 0m : Math.Round(completionCount * 100m / possibleCompletions, 1);
                return new AdminAnalyticsCourseResponse(course.Id, course.Title, course.Status.ToString(), enrollments, rate);
            })
            .OrderByDescending(course => course.Enrollments)
            .ThenBy(course => course.Title)
            .Take(8)
            .ToArray();

        var monthly = Enumerable.Range(0, 12).Select(offset => trendStart.AddMonths(offset)).Select(month =>
        {
            int FindCount<T>(IReadOnlyCollection<T> rows, Func<T, int> year, Func<T, int> monthNumber, Func<T, int> count) =>
                rows.Where(row => year(row) == month.Year && monthNumber(row) == month.Month).Select(count).FirstOrDefault();

            return new AdminAnalyticsMonthResponse(
                month,
                FindCount(userTrend, row => row.Year, row => row.Month, row => row.Count),
                FindCount(enrollmentTrend, row => row.Year, row => row.Month, row => row.Count),
                FindCount(submissionTrend, row => row.Year, row => row.Month, row => row.Count));
        }).ToArray();

        return new AdminAnalyticsResponse(
            now, days, rangeStart,
            totalUsers, activeUsers, students, teachers,
            courses.Count,
            courses.Count(item => item.Status == CourseStatus.Published),
            courses.Count(item => item.Status == CourseStatus.PendingApproval),
            courses.Count(item => item.Status == CourseStatus.Archived),
            totalEnrollments, completionRate,
            newUsersInRange, enrollmentsInRange, submissionsInRange,
            totalSubmissions, gradedSubmissions, averageGrade,
            monthly, topCourses);
    }
}
