using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments");
        builder.HasKey(enrollment => enrollment.Id);
        builder.HasOne<Course>().WithMany().HasForeignKey(enrollment => enrollment.CourseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(enrollment => enrollment.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(enrollment => new { enrollment.StudentUserId, enrollment.CourseId }).IsUnique();
        builder.HasIndex(enrollment => enrollment.CourseId);
    }
}
