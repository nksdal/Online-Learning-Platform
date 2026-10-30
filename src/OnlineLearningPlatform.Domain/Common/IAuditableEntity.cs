namespace OnlineLearningPlatform.Domain.Common;

/// <summary>
/// Implemented by entities whose creation and last-modification times (UTC) are stamped
/// automatically by the DbContext when the entity is saved. Application code never sets them.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }

    DateTime UpdatedAtUtc { get; set; }
}
