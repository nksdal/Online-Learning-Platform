using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public sealed class Lesson : IAuditableEntity
{
    public const int TitleMaxLength = 150;
    public const int ContentMaxLength = 20_000;

    private Lesson() { }

    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
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
}
