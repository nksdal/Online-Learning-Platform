using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class CourseDiscussionPostConfiguration : IEntityTypeConfiguration<CourseDiscussionPost>
{
    public void Configure(EntityTypeBuilder<CourseDiscussionPost> builder)
    {
        builder.ToTable("CourseDiscussionPosts");
        builder.HasKey(post => post.Id);
        builder.Property(post => post.Body).IsRequired().HasMaxLength(CourseDiscussionPost.BodyMaxLength);
        builder.Property(post => post.ModerationReason).HasMaxLength(CourseDiscussionPost.ModerationReasonMaxLength);
        builder.HasOne<Course>().WithMany().HasForeignKey(post => post.CourseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(post => post.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseDiscussionPost>().WithMany().HasForeignKey(post => post.ParentPostId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(post => post.ModeratedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(post => new { post.CourseId, post.CreatedAtUtc });
        builder.HasIndex(post => post.ParentPostId);
    }
}
