using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Identity;

/// <summary>
/// One refresh token of one login session. Only a hash of the token is stored; the raw value
/// exists only in the user's HttpOnly cookie. Tokens are single-use: refreshing revokes the old
/// token and creates a new one in the same <see cref="FamilyId"/>. If an already-revoked token is
/// presented again, the whole family can be revoked (reuse detection).
/// </summary>
public sealed class RefreshToken
{
    public const int TokenHashMaxLength = 128;
    public const int RevokedReasonMaxLength = 64;
    public const int IpAddressMaxLength = 45; // longest textual IPv6 address
    public const int UserAgentMaxLength = 256;

    private RefreshToken()
    {
        // Required by EF Core. Use Create().
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Identifies the login session. Constant across rotations; new on every login.</summary>
    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? RevokedReason { get; private set; }

    /// <summary>The token that replaced this one when it was rotated. Not a foreign key (see docs/database.md).</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? UserAgent { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    /// <summary>A token is expired at the exact expiry instant, not one tick later.</summary>
    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsActive(DateTime nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    public static RefreshToken Create(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime nowUtc,
        TimeSpan lifetime,
        string? createdByIp,
        string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        Guard.EnsureMaxLength(tokenHash, TokenHashMaxLength, nameof(tokenHash));

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(lifetime),
            CreatedByIp = Guard.Truncate(createdByIp, IpAddressMaxLength),
            UserAgent = Guard.Truncate(userAgent, UserAgentMaxLength),
        };
    }

    /// <summary>
    /// Revokes the token. Revoking an already-revoked token changes nothing, so the first
    /// revocation (the true reason and time) is preserved for audit purposes.
    /// </summary>
    public void Revoke(DateTime nowUtc, string reason, Guid? replacedByTokenId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        Guard.EnsureMaxLength(reason, RevokedReasonMaxLength, nameof(reason));

        if (IsRevoked)
        {
            return;
        }

        RevokedAtUtc = nowUtc;
        RevokedReason = reason;
        ReplacedByTokenId = replacedByTokenId;
    }
}
