using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Catalog;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).IsRequired().HasMaxLength(Category.NameMaxLength);
        builder.Property(category => category.Description).HasMaxLength(Category.DescriptionMaxLength);
        builder.HasIndex(category => category.Name).IsUnique();
        builder.HasIndex(category => category.IsActive);
    }
}
