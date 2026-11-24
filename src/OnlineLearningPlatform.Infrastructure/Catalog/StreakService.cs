using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class StreakService(ApplicationDbContext db, TimeProvider clock) : IStreakService
{
    public async Task<StreakResponse> GetMyStreakAsync(Guid studentId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == studentId, ct)
            ?? throw new NotFoundException("User was not found.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        var timestamps = await db.LessonProgress.AsNoTracking()
            .Where(progress => progress.StudentUserId == studentId)
            .Select(progress => progress.CompletedAtUtc).ToListAsync(ct);
        var days = timestamps.Select(timestamp => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)), zone).DateTime))
            .Where(day => day <= today).Distinct().OrderBy(day => day).ToArray();
        var daySet = days.ToHashSet();

        var current = 0;
        var cursor = daySet.Contains(today) ? today : today.AddDays(-1);
        while (daySet.Contains(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        var longest = 0;
        var run = 0;
        DateOnly? previous = null;
        foreach (var day in days)
        {
            run = previous.HasValue && day.DayNumber == previous.Value.DayNumber + 1 ? run + 1 : 1;
            longest = Math.Max(longest, run);
            previous = day;
        }

        var recent = days.Where(day => day >= today.AddDays(-29)).ToArray();
        return new StreakResponse(zone.Id, current, longest, daySet.Contains(today),
            days.Length == 0 ? null : days[^1], recent);
    }
}
