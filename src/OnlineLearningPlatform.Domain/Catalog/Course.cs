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

public sealed class Course : IAuditableEntity
{
    public const int TitleMaxLength = 150;
    public const int SummaryMaxLength = 300;
    public const int DescriptionMaxLength = 10_000;
    public const int ImageUrlMaxLength = 2_048;
    public const int RejectionReasonMaxLength = 1_000;

    private Course() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid InstructorUserId { get; private set; }
    public CourseStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public static Course Create(
        Guid instructorUserId, Guid categoryId, string title, string summary, string description, string? imageUrl = null)
    {
        if (instructorUserId == Guid.Empty) throw new ArgumentException("Instructor is required.", nameof(instructorUserId));
        if (categoryId == Guid.Empty) throw new ArgumentException("Category is required.", nameof(categoryId));
        var course = new Course { Id = Guid.NewGuid(), InstructorUserId = instructorUserId, Status = CourseStatus.Draft };
        course.Update(categoryId, title, summary, description, imageUrl);
        return course;
    }

    public void Update(Guid categoryId, string title, string summary, string description, string? imageUrl = null)
    {
        if (Status is CourseStatus.PendingApproval or CourseStatus.Archived)
            throw new InvalidOperationException("This course cannot be edited in its current status.");
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
        CategoryId = categoryId;
        SetImageUrl(imageUrl);
    }

    public void SetImageUrl(string? imageUrl)
    {
        var trimmedImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        if (trimmedImageUrl is not null)
            Guard.EnsureMaxLength(trimmedImageUrl, ImageUrlMaxLength, nameof(imageUrl));
        ImageUrl = trimmedImageUrl;
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
