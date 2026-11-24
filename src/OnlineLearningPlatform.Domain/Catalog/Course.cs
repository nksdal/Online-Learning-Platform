using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public enum CourseStatus
{
    Draft,
    PendingApproval,
    Published,
    Rejected,
    Archived,
}

public enum CourseDifficulty
{
    Beginner,
    Intermediate,
    Advanced,
}

public sealed class Course : IAuditableEntity
{
    public const int TitleMaxLength = 150;
    public const int SummaryMaxLength = 300;
    public const int DescriptionMaxLength = 10_000;
    public const int ImageUrlMaxLength = 2_048;
    public const int DurationMaxLength = 100;
    public const int PrerequisitesMaxLength = 2_000;
    public const int CloudinaryImagePublicIdMaxLength = 255;
    public const int RejectionReasonMaxLength = 1_000;

    private Course() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public string? CloudinaryImagePublicId { get; private set; }
    public string Duration { get; private set; } = "Self-paced";
    public decimal Price { get; private set; }
    public CourseDifficulty Difficulty { get; private set; } = CourseDifficulty.Beginner;
    public string? Prerequisites { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid InstructorUserId { get; private set; }
    public CourseStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static Course Create(
        Guid instructorUserId,
        Guid categoryId,
        string title,
        string summary,
        string description,
        string? imageUrl = null,
        string duration = "Self-paced",
        decimal price = 0m,
        CourseDifficulty difficulty = CourseDifficulty.Beginner,
        string? prerequisites = null)
    {
        if (instructorUserId == Guid.Empty) throw new ArgumentException("Instructor is required.", nameof(instructorUserId));
        if (categoryId == Guid.Empty) throw new ArgumentException("Category is required.", nameof(categoryId));
        var course = new Course { Id = Guid.NewGuid(), InstructorUserId = instructorUserId, Status = CourseStatus.Draft };
        course.Update(categoryId, title, summary, description, imageUrl, duration, price, difficulty, prerequisites);
        return course;
    }

    public string? Update(
        Guid categoryId,
        string title,
        string summary,
        string description,
        string? imageUrl = null,
        string duration = "Self-paced",
        decimal price = 0m,
        CourseDifficulty difficulty = CourseDifficulty.Beginner,
        string? prerequisites = null)
    {
        if (Status is CourseStatus.PendingApproval or CourseStatus.Archived)
            throw new InvalidOperationException("This course cannot be edited in its current status.");
        UpdateDetails(categoryId, title, summary, description, duration, price, difficulty, prerequisites);
        return SetImageUrl(imageUrl);
    }

    /// <summary>Administrators can correct course content without changing its moderation status.</summary>
    public void UpdateByAdmin(
        Guid categoryId,
        string title,
        string summary,
        string description,
        string duration = "Self-paced",
        decimal price = 0m,
        CourseDifficulty difficulty = CourseDifficulty.Beginner,
        string? prerequisites = null) =>
        UpdateDetails(categoryId, title, summary, description, duration, price, difficulty, prerequisites);

    public string? SetImageUrl(string? imageUrl)
    {
        var trimmedImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        if (trimmedImageUrl is not null)
            Guard.EnsureMaxLength(trimmedImageUrl, ImageUrlMaxLength, nameof(imageUrl));

        var previousPublicId = string.Equals(ImageUrl, trimmedImageUrl, StringComparison.Ordinal)
            ? null
            : CloudinaryImagePublicId;
        if (previousPublicId is not null)
            CloudinaryImagePublicId = null;
        ImageUrl = trimmedImageUrl;
        return previousPublicId;
    }

    public string? SetCloudinaryImage(string secureUrl, string publicId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secureUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);
        if (!Uri.TryCreate(secureUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Course image URL must be a valid HTTPS URL.", nameof(secureUrl));
        Guard.EnsureMaxLength(publicId, CloudinaryImagePublicIdMaxLength, nameof(publicId));

        var previousPublicId = CloudinaryImagePublicId;
        ImageUrl = secureUrl.Trim();
        CloudinaryImagePublicId = publicId.Trim();
        return previousPublicId;
    }

    private void UpdateDetails(
        Guid categoryId,
        string title,
        string summary,
        string description,
        string duration,
        decimal price,
        CourseDifficulty difficulty,
        string? prerequisites)
    {
        if (categoryId == Guid.Empty) throw new ArgumentException("Category is required.", nameof(categoryId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Title = title.Trim();
        Summary = summary.Trim();
        Description = description.Trim();
        Guard.EnsureMaxLength(Title, TitleMaxLength, nameof(title));
        Guard.EnsureMaxLength(Summary, SummaryMaxLength, nameof(summary));
        Guard.EnsureMaxLength(Description, DescriptionMaxLength, nameof(description));
        ArgumentException.ThrowIfNullOrWhiteSpace(duration);
        Duration = duration.Trim();
        Guard.EnsureMaxLength(Duration, DurationMaxLength, nameof(duration));
        if (price is < 0m or > 99_999_999.99m || decimal.Round(price, 2) != price)
            throw new ArgumentOutOfRangeException(nameof(price), "Course price must be between 0 and 99,999,999.99 with at most two decimal places.");
        if (!Enum.IsDefined(difficulty))
            throw new ArgumentOutOfRangeException(nameof(difficulty), "Choose a valid course difficulty.");
        Price = price;
        Difficulty = difficulty;
        Prerequisites = string.IsNullOrWhiteSpace(prerequisites) ? null : prerequisites.Trim();
        if (Prerequisites is not null)
            Guard.EnsureMaxLength(Prerequisites, PrerequisitesMaxLength, nameof(prerequisites));
        CategoryId = categoryId;
    }

    public void SubmitForReview()
    {
        if (Status is not (CourseStatus.Draft or CourseStatus.Rejected))
            throw new InvalidOperationException("Only a draft or rejected course can be submitted for review.");
        Status = CourseStatus.PendingApproval;
        RejectionReason = null;
    }

    public void Approve()
    {
        if (Status != CourseStatus.PendingApproval)
            throw new InvalidOperationException("Only a course pending approval can be approved.");
        Status = CourseStatus.Published;
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        if (Status != CourseStatus.PendingApproval)
            throw new InvalidOperationException("Only a course pending approval can be rejected.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        RejectionReason = reason.Trim();
        Guard.EnsureMaxLength(RejectionReason, RejectionReasonMaxLength, nameof(reason));
        Status = CourseStatus.Rejected;
    }

    public void Archive()
    {
        if (Status != CourseStatus.Published)
            throw new InvalidOperationException("Only a published course can be archived.");
        Status = CourseStatus.Archived;
    }
}
