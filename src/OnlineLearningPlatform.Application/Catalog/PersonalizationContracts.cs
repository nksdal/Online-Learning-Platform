namespace OnlineLearningPlatform.Application.Catalog;

public sealed record StreakResponse(
    string TimeZoneId,
    int CurrentStreakDays,
    int LongestStreakDays,
    bool LearnedToday,
    DateOnly? LastLearningDayLocal,
    IReadOnlyCollection<DateOnly> RecentLearningDays);

public sealed record RecommendedCourseResponse(
    Guid CourseId, string Title, string Summary, Guid CategoryId, string? ImageUrl, int EnrollmentCount, string Reason);

public interface IStreakService
{
    Task<StreakResponse> GetMyStreakAsync(Guid studentId, CancellationToken ct);
}

public interface IRecommendationService
{
    Task<IReadOnlyCollection<RecommendedCourseResponse>> RecommendAsync(Guid studentId, int limit, CancellationToken ct);
}
