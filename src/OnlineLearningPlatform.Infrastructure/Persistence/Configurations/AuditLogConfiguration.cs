using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearningPlatform.Domain.Auditing;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id); // long key => IDENTITY column, cheap for an append-only table

        builder.Property(a => a.Action).IsRequired().HasMaxLength(AuditLog.ActionMaxLength);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(AuditLog.EntityTypeMaxLength);
        builder.Property(a => a.EntityId).HasMaxLength(AuditLog.EntityIdMaxLength);
        builder.Property(a => a.Details).HasMaxLength(AuditLog.DetailsMaxLength);
        builder.Property(a => a.IpAddress).HasMaxLength(AuditLog.IpAddressMaxLength);

        builder.HasIndex(a => a.OccurredAtUtc);
        builder.HasIndex(a => a.ActorUserId);

        // ActorUserId is intentionally NOT a foreign key: an audit record must outlive the user it
        // describes, and must never block (or be cascaded away by) deleting that user.
    }
}
