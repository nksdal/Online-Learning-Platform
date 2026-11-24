using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class RecommendationService(ApplicationDbContext db) : IRecommendationService
{
    public async Task<IReadOnlyCollection<RecommendedCourseResponse>> RecommendAsync(
        Guid studentId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 20);

        var enrolledCourseIds = (await db.Enrollments.AsNoTracking()
                .Where(enrollment => enrollment.StudentUserId == studentId)
                .Select(enrollment => enrollment.CourseId)
                .ToArrayAsync(ct))
            .ToHashSet();

        var enrolledCategoryIds = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.StudentUserId == studentId)
            .Join(db.Courses.AsNoTracking(), enrollment => enrollment.CourseId, course => course.Id,
                (_, course) => course.CategoryId)
            .ToArrayAsync(ct);
        var categoryAffinity = enrolledCategoryIds
            .GroupBy(categoryId => categoryId)
            .ToDictionary(group => group.Key, group => group.Count());

        var popularity = await db.Enrollments.AsNoTracking()
            .GroupBy(enrollment => enrollment.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CourseId, item => item.Count, ct);

        // Keep SQL limited to simple, provider-friendly reads. Ranking is intentionally done
        // in memory because the SQL Server provider cannot translate the previous grouped
        // left-join ordering for courses with no learner history.
        var publishedCourses = await db.Courses.AsNoTracking()
            .Where(course => course.Status == Domain.Catalog.CourseStatus.Published)
            .Select(course => new RecommendationCandidate(
                course.Id, course.Title, course.Summary, course.CategoryId, course.ImageUrl))
            .ToArrayAsync(ct);

        return publishedCourses
            .Where(course => !enrolledCourseIds.Contains(course.CourseId))
            .OrderByDescending(course => categoryAffinity.GetValueOrDefault(course.CategoryId))
            .ThenByDescending(course => popularity.GetValueOrDefault(course.CourseId))
            .ThenBy(course => course.Title)
            .Take(limit)
            .Select(course => new RecommendedCourseResponse(
                course.CourseId,
                course.Title,
                course.Summary,
                course.CategoryId,
                course.ImageUrl,
                popularity.GetValueOrDefault(course.CourseId),
                categoryAffinity.ContainsKey(course.CategoryId)
                    ? "Based on your enrolled course categories."
                    : "Popular with learners."))
            .ToArray();
    }

    private sealed record RecommendationCandidate(
        Guid CourseId, string Title, string Summary, Guid CategoryId, string? ImageUrl);
}
