using FluentValidation;

namespace OnlineLearningPlatform.Application.Catalog;

public sealed record CategoryWriteRequest(string Name, string? Description);

public sealed class CategoryWriteRequestValidator : AbstractValidator<CategoryWriteRequest>
{
    public CategoryWriteRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Description).MaximumLength(500);
    }
}

public sealed record CategoryStatusRequest(bool IsActive);

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public interface ICategoryService
{
    Task<IReadOnlyCollection<CategoryResponse>> ListActiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CategoryResponse>> ListAllAsync(CancellationToken cancellationToken);

    Task<CategoryResponse> CreateAsync(Guid actorUserId, string? ipAddress, CategoryWriteRequest request, CancellationToken cancellationToken);

    Task<CategoryResponse> UpdateAsync(Guid actorUserId, string? ipAddress, Guid categoryId, CategoryWriteRequest request, CancellationToken cancellationToken);

    Task<CategoryResponse> SetActiveAsync(Guid actorUserId, string? ipAddress, Guid categoryId, bool isActive, CancellationToken cancellationToken);
}
