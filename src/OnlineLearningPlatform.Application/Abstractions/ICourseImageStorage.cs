namespace OnlineLearningPlatform.Application.Abstractions;

public sealed record StoredCourseImage(string SecureUrl, string PublicId);

public interface ICourseImageStorage
{
    Task<StoredCourseImage> UploadAsync(
        Guid courseId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken);

    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}

public sealed class CourseImageStorageNotConfiguredException : InvalidOperationException
{
    public CourseImageStorageNotConfiguredException()
        : base("Course image storage is not configured.")
    {
    }
}
