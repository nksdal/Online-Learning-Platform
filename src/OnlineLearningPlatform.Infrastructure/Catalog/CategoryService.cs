using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Domain.Auditing;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class CategoryService(ApplicationDbContext dbContext, TimeProvider timeProvider) : ICategoryService
{
    public async Task<IReadOnlyCollection<CategoryResponse>> ListActiveAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => ToResponse(category))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<CategoryResponse>> ListAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => ToResponse(category))
            .ToListAsync(cancellationToken);

    public async Task<CategoryResponse> CreateAsync(
        Guid actorUserId,
        string? ipAddress,
        CategoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var normalizedName = name.ToUpperInvariant();
        if (await dbContext.Categories.AnyAsync(
                category => category.Name.ToUpper() == normalizedName,
                cancellationToken))
        {
            throw new ConflictException("A category with this name already exists.");
        }

        var category = Category.Create(name, request.Description);
        dbContext.Categories.Add(category);
        AddAudit(actorUserId, ipAddress, "CategoryCreated", category, $"Category '{category.Name}' was created.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateAsync(
        Guid actorUserId,
        string? ipAddress,
        Guid categoryId,
        CategoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        var name = request.Name.Trim();
        var normalizedName = name.ToUpperInvariant();
        if (await dbContext.Categories.AnyAsync(
                item => item.Id != categoryId && item.Name.ToUpper() == normalizedName,
                cancellationToken))
        {
            throw new ConflictException("A category with this name already exists.");
        }

        var previousName = category.Name;
        category.Update(name, request.Description);
        AddAudit(actorUserId, ipAddress, "CategoryUpdated", category,
            $"Category changed from '{previousName}' to '{category.Name}'.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(category);
    }

    public async Task<CategoryResponse> SetActiveAsync(
        Guid actorUserId,
        string? ipAddress,
        Guid categoryId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        if (category.IsActive != isActive)
        {
            category.SetActive(isActive);
            AddAudit(actorUserId, ipAddress, isActive ? "CategoryActivated" : "CategoryDeactivated", category,
                $"Category active state changed to {isActive}.");
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(category);
    }

    private void AddAudit(Guid actorUserId, string? ipAddress, string action, Category category, string details) =>
        dbContext.AuditLogs.Add(AuditLog.Create(
            timeProvider.GetUtcNow().UtcDateTime,
            action,
            nameof(Category),
            actorUserId,
            category.Id.ToString(),
            details,
            ipAddress));

    private static CategoryResponse ToResponse(Category category) => new(
        category.Id,
        category.Name,
        category.Description,
        category.IsActive,
        category.CreatedAtUtc,
        category.UpdatedAtUtc);
}
