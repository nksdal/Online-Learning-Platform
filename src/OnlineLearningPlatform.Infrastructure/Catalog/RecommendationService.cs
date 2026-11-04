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
        var enrolledIds = await db.Enrollments.AsNoTracking().Where(item => item.StudentUserId == studentId)
            .Select(item => item.CourseId).ToArrayAsync(ct);
        var categoryHistory = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.StudentUserId == studentId)
            .Join(db.Courses.AsNoTracking(), enrollment => enrollment.CourseId, course => course.Id,
                (_, course) => course.CategoryId)
            .ToArrayAsync(ct);
        var categoryAffinity = categoryHistory.GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
        var popularity = await db.Enrollments.AsNoTracking().GroupBy(item => item.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CourseId, item => item.Count, ct);
        var candidates = await db.Courses.AsNoTracking()
            .Where(course => course.Status == Domain.Catalog.CourseStatus.Published && !enrolledIds.Contains(course.Id))
            .ToListAsync(ct);

        return candidates
            .OrderByDescending(course => categoryAffinity.GetValueOrDefault(course.CategoryId))
            .ThenByDescending(course => popularity.GetValueOrDefault(course.Id))
            .ThenBy(course => course.Title)
            .Take(limit)
            .Select(course => new RecommendedCourseResponse(
                course.Id,
                course.Title,
                course.Summary,
                course.CategoryId,
                course.ImageUrl,
                popularity.GetValueOrDefault(course.Id),
                categoryAffinity.ContainsKey(course.CategoryId) ? "Based on your enrolled course categories." : "Popular with learners."))
            .ToArray();
    }
}
