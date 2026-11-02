using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> builder)
    {
        builder.ToTable("LessonProgress");
        builder.HasKey(progress => progress.Id);
        builder.HasOne<Lesson>().WithMany().HasForeignKey(progress => progress.LessonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(progress => progress.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(progress => new { progress.StudentUserId, progress.LessonId }).IsUnique();
    }
}
