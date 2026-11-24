using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Auditing;

/// <summary>
/// Append-only record of a security- or administration-relevant event (role changes, account
/// suspension, token-reuse detection, ...). Never put secrets, passwords or tokens in <see cref="Details"/>.
/// </summary>
public sealed class AuditLog
{
    public const int ActionMaxLength = 100;
    public const int EntityTypeMaxLength = 100;
    public const int EntityIdMaxLength = 64;
    public const int DetailsMaxLength = 2000;
    public const int IpAddressMaxLength = 45;

    private AuditLog()
    {
        // Required by EF Core. Use Create().
    }

    public long Id { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Who did it. Null for system actions. Deliberately not a foreign key.</summary>
    public Guid? ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public string? Details { get; private set; }

    public string? IpAddress { get; private set; }

    public static AuditLog Create(
        DateTime occurredAtUtc,
        string action,
        string entityType,
        Guid? actorUserId = null,
        string? entityId = null,
        string? details = null,
        string? ipAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        Guard.EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        Guard.EnsureMaxLength(action, ActionMaxLength, nameof(action));
        Guard.EnsureMaxLength(entityType, EntityTypeMaxLength, nameof(entityType));

        return new AuditLog
        {
            OccurredAtUtc = occurredAtUtc,
            Action = action,
            EntityType = entityType,
            ActorUserId = actorUserId,
            EntityId = Guard.Truncate(entityId, EntityIdMaxLength),
            Details = Guard.Truncate(details, DetailsMaxLength),
            IpAddress = Guard.Truncate(ipAddress, IpAddressMaxLength),
        };
    }
}
