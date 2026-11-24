using OnlineLearningPlatform.Domain.Catalog;

namespace OnlineLearningPlatform.UnitTests.Catalog;

public sealed class CourseTests
{
    [Fact]
    public void Rejected_course_can_be_edited_and_resubmitted()
    {
        var course = NewCourse();
        course.SubmitForReview();
        course.Reject("Please add clearer learning outcomes.");

        course.Update(Guid.NewGuid(), "Updated title", "Updated summary", "Updated description");
        Assert.Equal(CourseStatus.Rejected, course.Status);
        Assert.Equal("Please add clearer learning outcomes.", course.RejectionReason);

        course.SubmitForReview();
        Assert.Equal(CourseStatus.PendingApproval, course.Status);
        Assert.Null(course.RejectionReason);
    }

    [Fact]
    public void Archived_course_is_terminal()
    {
        var course = NewCourse();
        course.SubmitForReview();
        course.Approve();
        course.Archive();

        Assert.Throws<InvalidOperationException>(() => course.Update(Guid.NewGuid(), "Title", "Summary", "Description"));
        Assert.Throws<InvalidOperationException>(course.SubmitForReview);
        Assert.Throws<InvalidOperationException>(course.Approve);
        Assert.Throws<InvalidOperationException>(course.Archive);
    }

    private static Course NewCourse() =>
        Course.Create(Guid.NewGuid(), Guid.NewGuid(), "Title", "Summary", "Description");
}
