using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(ApplicationUser.NameMaxLength);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(ApplicationUser.NameMaxLength);
        builder.Property(u => u.TimeZoneId).IsRequired().HasMaxLength(ApplicationUser.TimeZoneIdMaxLength);
        builder.Property(u => u.ProfileImageUrl).HasMaxLength(ApplicationUser.ProfileImageUrlMaxLength);
        builder.Property(u => u.ProfileImagePublicId).HasMaxLength(ApplicationUser.ProfileImagePublicIdMaxLength);

        // Identity creates a NON-unique index on NormalizedEmail and relies on UserManager to reject
        // duplicates. Two simultaneous registrations could slip past that check, so the database
        // enforces it too. (Same index, same name as Identity's - we only add "unique".)
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
    }
}
