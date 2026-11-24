namespace OnlineLearningPlatform.Application.Abstractions;

public interface IAccessTokenService
{
    IssuedAccessToken Create(
        Guid userId,
        string email,
        IReadOnlyCollection<string> roles,
        bool mustChangePassword = false,
        string? securityStamp = null);
}

public sealed record IssuedAccessToken(string Value, DateTime ExpiresAtUtc);
