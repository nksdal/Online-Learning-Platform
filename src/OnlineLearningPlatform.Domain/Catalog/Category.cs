using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Catalog;

public sealed class Category : IAuditableEntity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private Category()
    {
        // Required by EF Core. Use Create().
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public static Category Create(string name, string? description)
    {
        var category = new Category { Id = Guid.NewGuid(), IsActive = true };
        category.Update(name, description);
        return category;
    }

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmedName = name.Trim();
        Guard.EnsureMaxLength(trimmedName, NameMaxLength, nameof(name));
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException("Category name cannot be blank.", nameof(name));
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null)
        {
            Guard.EnsureMaxLength(trimmedDescription, DescriptionMaxLength, nameof(description));
        }
        Name = trimmedName;
        Description = trimmedDescription;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
