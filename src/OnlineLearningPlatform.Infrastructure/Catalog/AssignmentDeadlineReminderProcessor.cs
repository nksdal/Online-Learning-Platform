using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearningPlatform.Application.Authentication;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class AssignmentDeadlineReminderOptions
{
    public const string SectionName = "AssignmentDeadlineReminders";
    public int PollIntervalMinutes { get; set; } = 15;
    public int ReminderWindowHours { get; set; } = 24;
}

public interface IAssignmentDeadlineReminderProcessor
{
    Task ProcessOnceAsync(CancellationToken cancellationToken);
}

public sealed class AssignmentDeadlineReminderProcessor(
    ApplicationDbContext db,
    IEmailSender emailSender,
    TimeProvider clock,
    IOptions<AssignmentDeadlineReminderOptions> options,
    ILogger<AssignmentDeadlineReminderProcessor> logger) : IAssignmentDeadlineReminderProcessor
{
    public async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var windowEnd = now.AddHours(options.Value.ReminderWindowHours);
        var targets = await (
            from assignment in db.Assignments.AsNoTracking()
            join course in db.Courses.AsNoTracking() on assignment.CourseId equals course.Id
            join enrollment in db.Enrollments.AsNoTracking() on course.Id equals enrollment.CourseId
            join user in db.Users.AsNoTracking() on enrollment.StudentUserId equals user.Id
            where assignment.DueAtUtc.HasValue && assignment.DueAtUtc > now && assignment.DueAtUtc <= windowEnd
                  && course.Status == CourseStatus.Published && user.IsActive && user.EmailConfirmed
                  && !db.AssignmentSubmissions.Any(submission =>
                      submission.AssignmentId == assignment.Id && submission.StudentUserId == user.Id)
            select new ReminderTarget(assignment.Id, user.Id, user.Email!, user.FirstName,
                assignment.Title, course.Title, assignment.DueAtUtc!.Value))
            .ToListAsync(cancellationToken);

        var assignmentIds = targets.Select(target => target.AssignmentId).Distinct().ToArray();
        var existing = assignmentIds.Length == 0
            ? []
            : await db.AssignmentDeadlineReminders.AsNoTracking()
                .Where(reminder => assignmentIds.Contains(reminder.AssignmentId))
                .Select(reminder => new ReminderKey(reminder.AssignmentId, reminder.StudentUserId))
                .ToListAsync(cancellationToken);
        var known = existing.ToHashSet();
        foreach (var target in targets)
        {
            if (known.Add(new ReminderKey(target.AssignmentId, target.StudentUserId)))
                db.AssignmentDeadlineReminders.Add(AssignmentDeadlineReminder.Create(target.AssignmentId, target.StudentUserId));
        }
        await db.SaveChangesAsync(cancellationToken);

        var pending = await (
            from reminder in db.AssignmentDeadlineReminders
            join assignment in db.Assignments on reminder.AssignmentId equals assignment.Id
            join course in db.Courses on assignment.CourseId equals course.Id
            join user in db.Users on reminder.StudentUserId equals user.Id
            where reminder.SentAtUtc == null && reminder.AttemptCount < AssignmentDeadlineReminder.MaxDeliveryAttempts
                  && assignment.DueAtUtc.HasValue && assignment.DueAtUtc > now
                  && course.Status == CourseStatus.Published && user.IsActive && user.EmailConfirmed
                  && !db.AssignmentSubmissions.Any(submission =>
                      submission.AssignmentId == assignment.Id && submission.StudentUserId == user.Id)
            orderby assignment.DueAtUtc
            select new { Reminder = reminder, assignment.Title, CourseTitle = course.Title, user.Email, user.FirstName, assignment.DueAtUtc })
            .Take(50).ToListAsync(cancellationToken);

        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var dueText = item.DueAtUtc!.Value.ToString("yyyy-MM-dd HH:mm 'UTC'");
                var name = string.IsNullOrWhiteSpace(item.FirstName) ? "there" : HtmlEncoder.Default.Encode(item.FirstName);
                var subject = $"Assignment due soon: {item.Title}";
                var text = $"Hello {item.FirstName},\n\nThe assignment '{item.Title}' in '{item.CourseTitle}' is due at {dueText}.\nPlease sign in to Online Learning Platform to submit your work.";
                var html = $"<p>Hello {name},</p><p>The assignment <strong>{HtmlEncoder.Default.Encode(item.Title)}</strong> in <strong>{HtmlEncoder.Default.Encode(item.CourseTitle)}</strong> is due at {dueText}.</p><p>Please sign in to Online Learning Platform to submit your work.</p>";
                await emailSender.SendAsync(new EmailMessage(item.Email!, subject, text, html), cancellationToken);
                item.Reminder.MarkSent(clock.GetUtcNow().UtcDateTime);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                item.Reminder.RecordFailure(exception.GetType().Name);
                logger.LogWarning(exception, "Assignment deadline reminder delivery failed for reminder {ReminderId}.", item.Reminder.Id);
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed record ReminderTarget(Guid AssignmentId, Guid StudentUserId, string Email,
        string FirstName, string AssignmentTitle, string CourseTitle, DateTime DueAtUtc);
    private readonly record struct ReminderKey(Guid AssignmentId, Guid StudentUserId);
}
