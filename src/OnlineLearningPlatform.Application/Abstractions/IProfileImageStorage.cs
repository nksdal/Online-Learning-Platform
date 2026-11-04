namespace OnlineLearningPlatform.Application.Abstractions;

public sealed record StoredProfileImage(string SecureUrl, string PublicId);

public interface IProfileImageStorage
{
    Task<StoredProfileImage> UploadAsync(
        Guid userId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken);

    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}

public sealed class ProfileImageStorageNotConfiguredException : InvalidOperationException
{
    public ProfileImageStorageNotConfiguredException()
        : base("Profile image storage is not configured.")
    {
    }
}
