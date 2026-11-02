using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons");
        builder.HasKey(lesson => lesson.Id);
        builder.Property(lesson => lesson.Title).IsRequired().HasMaxLength(Lesson.TitleMaxLength);
        builder.Property(lesson => lesson.Content).IsRequired().HasMaxLength(Lesson.ContentMaxLength);
        builder.HasOne<Course>().WithMany().HasForeignKey(lesson => lesson.CourseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(lesson => new { lesson.CourseId, lesson.Position });
    }
}
