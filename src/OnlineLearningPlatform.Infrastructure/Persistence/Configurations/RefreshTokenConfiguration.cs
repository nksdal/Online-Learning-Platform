using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(RefreshToken.TokenHashMaxLength);
        builder.Property(t => t.RevokedReason).HasMaxLength(RefreshToken.RevokedReasonMaxLength);
        builder.Property(t => t.CreatedByIp).HasMaxLength(RefreshToken.IpAddressMaxLength);
        builder.Property(t => t.UserAgent).HasMaxLength(RefreshToken.UserAgentMaxLength);

        // Every refresh request looks the token up by its hash, and a hash must never exist twice.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Reuse detection revokes a whole session: "all tokens of this family".
        builder.HasIndex(t => t.FamilyId);

        // "List / revoke this user's active sessions". Also serves as the foreign-key index for UserId.
        builder.HasIndex(t => new { t.UserId, t.RevokedAtUtc });

        // Tokens have no meaning without their user, so they go when the user goes.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
