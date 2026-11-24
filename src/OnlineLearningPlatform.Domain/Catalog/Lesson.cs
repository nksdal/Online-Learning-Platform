using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public sealed class Lesson : IAuditableEntity
{
    public const int TitleMaxLength = 150;
    public const int ContentMaxLength = 20_000;
    public const int VideoUrlMaxLength = 2_048;
    public const int VideoPublicIdMaxLength = 255;

    private Lesson() { }

    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string? VideoUrl { get; private set; }
    public string? VideoPublicId { get; private set; }
    public int Position { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static Lesson Create(Guid courseId, string title, string content, int position)
    {
        if (courseId == Guid.Empty) throw new ArgumentException("Course is required.", nameof(courseId));
        var lesson = new Lesson { Id = Guid.NewGuid(), CourseId = courseId };
        lesson.Update(title, content, position);
        return lesson;
    }

    public void Update(string title, string content, int position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (position < 1) throw new ArgumentOutOfRangeException(nameof(position), "Lesson position must be at least 1.");
        Title = title.Trim();
        Content = content.Trim();
        Guard.EnsureMaxLength(Title, TitleMaxLength, nameof(title));
        Guard.EnsureMaxLength(Content, ContentMaxLength, nameof(content));
        Position = position;
    }

    public string? SetVideo(string secureUrl, string publicId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secureUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);
        if (!Uri.TryCreate(secureUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Lesson video URL must be a valid HTTPS URL.", nameof(secureUrl));
        Guard.EnsureMaxLength(secureUrl, VideoUrlMaxLength, nameof(secureUrl));
        Guard.EnsureMaxLength(publicId, VideoPublicIdMaxLength, nameof(publicId));
        var previousPublicId = VideoPublicId;
        VideoUrl = secureUrl.Trim();
        VideoPublicId = publicId.Trim();
        return previousPublicId;
    }

    public string? RemoveVideo()
    {
        var previousPublicId = VideoPublicId;
        VideoUrl = null;
        VideoPublicId = null;
        return previousPublicId;
    }
}
