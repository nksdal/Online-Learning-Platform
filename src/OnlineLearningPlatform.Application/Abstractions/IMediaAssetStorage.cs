namespace OnlineLearningPlatform.Application.Abstractions;

public enum MediaAssetResourceType
{
    Video,
    Raw,
}

public sealed record StoredMediaAsset(string SecureUrl, string PublicId);

/// <summary>Stores large course videos and learner attachments without exposing provider credentials.</summary>
public interface IMediaAssetStorage
{
    Task<StoredMediaAsset> UploadAsync(
        MediaAssetResourceType resourceType,
        string folder,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken,
        bool privateDelivery = false);

    Task DeleteAsync(
        MediaAssetResourceType resourceType, string publicId, CancellationToken cancellationToken,
        bool privateDelivery = false);

    string CreateTemporaryDownloadUrl(
        MediaAssetResourceType resourceType, string publicId, string format, TimeSpan lifetime);
}

public sealed class MediaAssetStorageNotConfiguredException : InvalidOperationException
{
    public MediaAssetStorageNotConfiguredException() : base("Cloudinary media storage is not configured.") { }
}
