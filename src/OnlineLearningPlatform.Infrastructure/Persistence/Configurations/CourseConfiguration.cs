using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses", table =>
        {
            table.HasCheckConstraint("CK_Courses_Price_NonNegative", "[Price] >= 0");
            table.HasCheckConstraint("CK_Courses_Difficulty_Values", "[Difficulty] IN ('Beginner', 'Intermediate', 'Advanced')");
        });
        builder.HasKey(course => course.Id);
        builder.Property(course => course.Title).IsRequired().HasMaxLength(Course.TitleMaxLength);
        builder.Property(course => course.Summary).IsRequired().HasMaxLength(Course.SummaryMaxLength);
        builder.Property(course => course.Description).IsRequired().HasMaxLength(Course.DescriptionMaxLength);
        builder.Property(course => course.ImageUrl).HasMaxLength(Course.ImageUrlMaxLength);
        builder.Property(course => course.CloudinaryImagePublicId).HasMaxLength(Course.CloudinaryImagePublicIdMaxLength);
        builder.Property(course => course.Duration).HasMaxLength(Course.DurationMaxLength).HasDefaultValue("Self-paced").IsRequired();
        builder.Property(course => course.Price).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(course => course.Difficulty).HasConversion<string>().HasMaxLength(24).HasDefaultValue(CourseDifficulty.Beginner).IsRequired();
        builder.Property(course => course.Prerequisites).HasMaxLength(Course.PrerequisitesMaxLength);
        builder.Property(course => course.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(course => course.RejectionReason).HasMaxLength(Course.RejectionReasonMaxLength);
        builder.HasOne<Category>().WithMany().HasForeignKey(course => course.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(course => course.InstructorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(course => course.Status);
        builder.HasIndex(course => course.InstructorUserId);
        builder.HasIndex(course => new { course.Status, course.CategoryId });
    }
}
