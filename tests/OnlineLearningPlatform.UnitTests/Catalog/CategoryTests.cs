using OnlineLearningPlatform.Domain.Catalog;

namespace OnlineLearningPlatform.UnitTests.Catalog;

public sealed class CategoryTests
{
    [Fact]
    public void Create_trims_name_and_normalizes_blank_description_to_null()
    {
        var category = Category.Create("  Backend  ", "   ");

        Assert.Equal("Backend", category.Name);
        Assert.Null(category.Description);
        Assert.True(category.IsActive);
        Assert.NotEqual(Guid.Empty, category.Id);
    }

    [Fact]
    public void Update_rejects_name_over_limit()
    {
        var category = Category.Create("Backend", null);

        Assert.Throws<ArgumentException>(() => category.Update(new string('a', Category.NameMaxLength + 1), null));
    }
}
