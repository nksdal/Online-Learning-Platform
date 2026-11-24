using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class AccountActionTokenConfiguration : IEntityTypeConfiguration<AccountActionToken>
{
    public void Configure(EntityTypeBuilder<AccountActionToken> builder)
    {
        builder.ToTable("AccountActionTokens");
        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(AccountActionToken.TokenHashMaxLength);
        builder.Property(token => token.Purpose)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(AccountActionToken.PurposeMaxLength);

        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.UserId, token.Purpose, token.UsedAtUtc });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
