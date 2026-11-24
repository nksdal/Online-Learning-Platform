using OnlineLearningPlatform.Domain.Catalog;

namespace OnlineLearningPlatform.UnitTests.Catalog;

public sealed class LessonTests
{
    [Fact]
    public void Lesson_trims_text_and_keeps_its_position()
    {
        var lesson = Lesson.Create(Guid.NewGuid(), "  Welcome  ", "  Start here.  ", 2);

        Assert.Equal("Welcome", lesson.Title);
        Assert.Equal("Start here.", lesson.Content);
        Assert.Equal(2, lesson.Position);
    }

    [Fact]
    public void Lesson_requires_a_positive_position()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Lesson.Create(Guid.NewGuid(), "Welcome", "Start here.", 0));
    }
}
