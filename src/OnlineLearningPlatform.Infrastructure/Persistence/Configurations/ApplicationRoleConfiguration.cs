using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

/// <summary>
/// Seeds the three roles as reference data inside the migration. Fixed ids and concurrency stamps keep
/// the seed deterministic: the same migration always produces the same rows, in every environment.
/// No user (and no password) is seeded here - the administrator is created at startup from configuration.
/// </summary>
public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.HasData(
            CreateRole("4f0a7c52-9b1e-4d3a-8c61-2e7b5a90d1f3", Roles.Student, "a3f1c5e9-7b2d-4a68-b0c4-9e8d1f2a3b50"),
            CreateRole("b8d2e6a1-3c47-4f95-a0b2-7d1e9c4f6a58", Roles.Teacher, "c7e2a4b6-1d93-4f58-8a0e-5b6c7d8e9f01"),
            CreateRole("e1c93b7d-5a28-4e06-9f4b-0a8d2c6e7b19", Roles.Admin, "d5b8f0a2-6c14-4e79-b3d1-8f9a0b1c2d34"));
    }

    private static ApplicationRole CreateRole(string id, string name, string concurrencyStamp) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(), // same normalisation Identity applies
        ConcurrencyStamp = concurrencyStamp,
    };
}
