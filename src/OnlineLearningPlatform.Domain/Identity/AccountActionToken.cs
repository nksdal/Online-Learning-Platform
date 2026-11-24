using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Identity;

/// <summary>A single-use verification or password-reset token. Only the SHA-256 hash is stored.</summary>
public sealed class AccountActionToken
{
    public const int TokenHashMaxLength = 64;
    public const int PurposeMaxLength = 32;

    private AccountActionToken()
    {
        // Required by EF Core. Use Create().
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public AccountActionTokenPurpose Purpose { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? UsedAtUtc { get; private set; }

    public bool IsUsable(DateTime nowUtc) => UsedAtUtc is null && nowUtc < ExpiresAtUtc;

    public static AccountActionToken Create(
        Guid userId,
        string tokenHash,
        AccountActionTokenPurpose purpose,
        DateTime nowUtc,
        TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfNotEqual(tokenHash.Length, TokenHashMaxLength);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        Guard.EnsureUtc(nowUtc, nameof(nowUtc));

        return new AccountActionToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            Purpose = purpose,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(lifetime),
        };
    }

    public void MarkUsed(DateTime nowUtc)
    {
        Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        UsedAtUtc ??= nowUtc;
    }
}

public enum AccountActionTokenPurpose
{
    EmailVerification,
    PasswordReset,
}
