using FluentValidation;
using Microsoft.AspNetCore.Http;
using OnlineLearningPlatform.Domain.Catalog;

namespace OnlineLearningPlatform.Api.Models;

/// <summary>Multipart form used to edit course details and optionally upload a cover image.</summary>
public sealed class AdminCourseUpdateForm
{
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = "Self-paced";
    public decimal Price { get; set; }
    public string Difficulty { get; set; } = "Beginner";
    public string? Prerequisites { get; set; }
    public IFormFile? Image { get; set; }
}

public sealed class AdminCourseUpdateFormValidator : AbstractValidator<AdminCourseUpdateForm>
{
    public AdminCourseUpdateFormValidator()
    {
        RuleFor(form => form.CategoryId).NotEmpty();
        RuleFor(form => form.Title).NotEmpty().MaximumLength(150);
        RuleFor(form => form.Summary).NotEmpty().MaximumLength(300);
        RuleFor(form => form.Description).NotEmpty().MaximumLength(10_000);
        RuleFor(form => form.Duration).NotEmpty().MaximumLength(100);
        RuleFor(form => form.Price).InclusiveBetween(0m, 99_999_999.99m)
            .Must(price => decimal.Round(price, 2) == price)
            .WithMessage("Price must be non-negative with at most two decimal places.");
        RuleFor(form => form.Difficulty).Must(value =>
                Enum.TryParse<CourseDifficulty>(value, ignoreCase: false, out var difficulty) && Enum.IsDefined(difficulty))
            .WithMessage("Difficulty must be Beginner, Intermediate, or Advanced.");
        RuleFor(form => form.Prerequisites).MaximumLength(2_000);
    }
}
