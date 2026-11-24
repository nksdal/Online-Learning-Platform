namespace OnlineLearningPlatform.Application.Catalog;

public sealed record AdminAnalyticsMonthResponse(
    DateTime MonthStartUtc, int NewUsers, int Enrollments, int Submissions);

public sealed record AdminAnalyticsCourseResponse(
    Guid CourseId, string Title, string Status, int Enrollments, decimal CompletionPercentage);

public sealed record AdminAnalyticsResponse(
    DateTime GeneratedAtUtc,
    int RangeDays,
    DateTime RangeStartUtc,
    int TotalUsers,
    int ActiveUsers,
    int Students,
    int Teachers,
    int TotalCourses,
    int PublishedCourses,
    int PendingCourses,
    int ArchivedCourses,
    int TotalEnrollments,
    decimal CompletionPercentage,
    int NewUsersInRange,
    int EnrollmentsInRange,
    int SubmissionsInRange,
    int TotalSubmissions,
    int GradedSubmissions,
    decimal? AverageGrade,
    IReadOnlyCollection<AdminAnalyticsMonthResponse> MonthlyActivity,
    IReadOnlyCollection<AdminAnalyticsCourseResponse> TopCourses);

public interface IAdminAnalyticsService
{
    Task<AdminAnalyticsResponse> GetAsync(int days, CancellationToken cancellationToken);
}
