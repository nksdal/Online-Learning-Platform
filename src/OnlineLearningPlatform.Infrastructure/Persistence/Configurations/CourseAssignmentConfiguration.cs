using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class CourseAssignmentConfiguration : IEntityTypeConfiguration<CourseAssignment>
{
    public void Configure(EntityTypeBuilder<CourseAssignment> builder)
    {
        builder.ToTable("Assignments", table => table.HasCheckConstraint(
            "CK_Assignments_MaxScore_Range", "[MaxScore] > 0 AND [MaxScore] <= 10.0"));
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Title).IsRequired().HasMaxLength(CourseAssignment.TitleMaxLength);
        builder.Property(assignment => assignment.Instructions).IsRequired().HasMaxLength(CourseAssignment.InstructionsMaxLength);
        builder.Property(assignment => assignment.MaxScore).HasPrecision(8, 2);
        builder.HasOne<Course>().WithMany().HasForeignKey(assignment => assignment.CourseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(assignment => new { assignment.CourseId, assignment.DueAtUtc });
    }
}
