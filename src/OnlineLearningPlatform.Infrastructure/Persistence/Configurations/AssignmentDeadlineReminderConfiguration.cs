using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class AssignmentDeadlineReminderConfiguration : IEntityTypeConfiguration<AssignmentDeadlineReminder>
{
    public void Configure(EntityTypeBuilder<AssignmentDeadlineReminder> builder)
    {
        builder.ToTable("AssignmentDeadlineReminders");
        builder.HasKey(reminder => reminder.Id);
        builder.Property(reminder => reminder.LastError).HasMaxLength(AssignmentDeadlineReminder.LastErrorMaxLength);
        builder.HasOne<CourseAssignment>().WithMany().HasForeignKey(reminder => reminder.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(reminder => reminder.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(reminder => new { reminder.AssignmentId, reminder.StudentUserId }).IsUnique();
        builder.HasIndex(reminder => new { reminder.SentAtUtc, reminder.AttemptCount });
    }
}
