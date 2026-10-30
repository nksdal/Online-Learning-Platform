using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.UnitTests.Identity;

public class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FamilyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static RefreshToken NewToken(TimeSpan? lifetime = null, string? userAgent = "UnitTest") =>
        RefreshToken.Create(UserId, FamilyId, "hash-value", Now, lifetime ?? TimeSpan.FromDays(7), "203.0.113.10", userAgent);

    [Fact]
    public void Create_sets_expiry_from_lifetime_and_starts_active()
    {
        var token = NewToken(TimeSpan.FromDays(7));

        Assert.Equal(Now, token.CreatedAtUtc);
        Assert.Equal(Now.AddDays(7), token.ExpiresAtUtc);
        Assert.False(token.IsRevoked);
        Assert.True(token.IsActive(Now));
    }

    [Fact]
    public void Token_is_expired_at_the_exact_expiry_instant()
    {
        var token = NewToken(TimeSpan.FromMinutes(10));
        var expiry = Now.AddMinutes(10);

        Assert.True(token.IsActive(expiry.AddTicks(-1)));
        Assert.True(token.IsExpired(expiry));
        Assert.False(token.IsActive(expiry));
    }

    [Fact]
    public void Revoke_marks_the_token_revoked_and_no_longer_active()
    {
        var token = NewToken();
        var revokedAt = Now.AddMinutes(5);

        token.Revoke(revokedAt, "LoggedOut");

        Assert.True(token.IsRevoked);
        Assert.Equal(revokedAt, token.RevokedAtUtc);
        Assert.Equal("LoggedOut", token.RevokedReason);
        Assert.Null(token.ReplacedByTokenId);
        Assert.False(token.IsActive(revokedAt));
    }

    [Fact]
    public void Revoking_twice_keeps_the_first_revocation()
    {
        var token = NewToken();
        var firstRevocation = Now.AddMinutes(1);

        token.Revoke(firstRevocation, "Rotated");
        token.Revoke(Now.AddMinutes(2), "ReuseDetected");

        Assert.Equal(firstRevocation, token.RevokedAtUtc);
        Assert.Equal("Rotated", token.RevokedReason);
    }

    [Fact]
    public void Revoke_records_the_replacement_token()
    {
        var token = NewToken();
        var successor = Guid.Parse("33333333-3333-3333-3333-333333333333");

        token.Revoke(Now.AddMinutes(1), "Rotated", successor);

        Assert.Equal(successor, token.ReplacedByTokenId);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Create_rejects_timestamps_that_are_not_explicitly_utc(DateTimeKind kind)
    {
        var notUtc = new DateTime(2026, 11, 2, 12, 0, 0, kind);

        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Create(UserId, FamilyId, "hash-value", notUtc, TimeSpan.FromDays(1), null, null));
    }

    [Fact]
    public void Revoke_rejects_timestamps_that_are_not_explicitly_utc()
    {
        var token = NewToken();
        var notUtc = new DateTime(2026, 11, 2, 12, 5, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => token.Revoke(notUtc, "LoggedOut"));
        Assert.False(token.IsRevoked);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_rejects_a_lifetime_that_is_not_positive(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewToken(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Create_truncates_an_overlong_user_agent_instead_of_failing()
    {
        var token = NewToken(userAgent: new string('x', RefreshToken.UserAgentMaxLength + 50));

        Assert.Equal(RefreshToken.UserAgentMaxLength, token.UserAgent!.Length);
    }
}
