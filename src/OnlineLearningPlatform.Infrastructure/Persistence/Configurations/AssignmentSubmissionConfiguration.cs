using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class AssignmentSubmissionConfiguration : IEntityTypeConfiguration<AssignmentSubmission>
{
    public void Configure(EntityTypeBuilder<AssignmentSubmission> builder)
    {
        builder.ToTable("AssignmentSubmissions", table => table.HasCheckConstraint(
            "CK_AssignmentSubmissions_Score_Range", "[Score] IS NULL OR ([Score] >= 0 AND [Score] <= 10.0)"));
        builder.HasKey(submission => submission.Id);
        builder.Property(submission => submission.Text).IsRequired().HasMaxLength(AssignmentSubmission.TextMaxLength);
        builder.Property(submission => submission.Feedback).HasMaxLength(AssignmentSubmission.FeedbackMaxLength);
        builder.Property(submission => submission.AttachmentUrl).HasMaxLength(AssignmentSubmission.AttachmentUrlMaxLength);
        builder.Property(submission => submission.AttachmentPublicId).HasMaxLength(AssignmentSubmission.AttachmentPublicIdMaxLength);
        builder.Property(submission => submission.AttachmentFileName).HasMaxLength(AssignmentSubmission.AttachmentFileNameMaxLength);
        builder.Property(submission => submission.AttachmentContentType).HasMaxLength(AssignmentSubmission.AttachmentContentTypeMaxLength);
        builder.Property(submission => submission.Score).HasPrecision(8, 2);
        builder.HasOne<CourseAssignment>().WithMany().HasForeignKey(submission => submission.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(submission => submission.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(submission => submission.GradedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(submission => new { submission.AssignmentId, submission.StudentUserId, submission.AttemptNumber }).IsUnique();
        builder.HasIndex(submission => new { submission.AssignmentId, submission.SubmittedAtUtc });
    }
}
