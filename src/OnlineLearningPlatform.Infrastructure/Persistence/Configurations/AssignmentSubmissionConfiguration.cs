using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class AssignmentSubmissionConfiguration : IEntityTypeConfiguration<AssignmentSubmission>
{
    public void Configure(EntityTypeBuilder<AssignmentSubmission> builder)
    {
        builder.ToTable("AssignmentSubmissions");
        builder.HasKey(submission => submission.Id);
        builder.Property(submission => submission.Text).IsRequired().HasMaxLength(AssignmentSubmission.TextMaxLength);
        builder.Property(submission => submission.Feedback).HasMaxLength(AssignmentSubmission.FeedbackMaxLength);
        builder.Property(submission => submission.Score).HasPrecision(8, 2);
        builder.HasOne<CourseAssignment>().WithMany().HasForeignKey(submission => submission.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(submission => submission.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(submission => submission.GradedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(submission => new { submission.AssignmentId, submission.StudentUserId, submission.AttemptNumber }).IsUnique();
        builder.HasIndex(submission => new { submission.AssignmentId, submission.SubmittedAtUtc });
    }
}
