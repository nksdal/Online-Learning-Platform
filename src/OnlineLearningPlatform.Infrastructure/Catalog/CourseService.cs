using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Domain.Auditing;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class CourseService(ApplicationDbContext db, TimeProvider clock) : ICourseService
{
    public async Task<IReadOnlyCollection<CourseResponse>> ListAllAsync(CancellationToken ct) =>
        (await db.Courses.AsNoTracking().OrderByDescending(course => course.UpdatedAtUtc).ToListAsync(ct))
            .Select(ToResponse).ToArray();

    public async Task<CourseResponse> GetAnyAsync(Guid courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == courseId, ct)
            ?? throw new NotFoundException("Course was not found.");
        return ToResponse(course);
    }

    public async Task<IReadOnlyCollection<CourseResponse>> ListPublishedAsync(Guid? categoryId, CancellationToken ct)
    {
        var query = db.Courses.AsNoTracking().Where(course => course.Status == CourseStatus.Published);
        if (categoryId.HasValue) query = query.Where(course => course.CategoryId == categoryId.Value);
        return (await query.OrderBy(course => course.Title).ToListAsync(ct)).Select(ToResponse).ToArray();
    }

    public async Task<CourseResponse> GetPublishedAsync(Guid courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == courseId && item.Status == CourseStatus.Published, ct)
            ?? throw new NotFoundException("Course was not found.");
        return ToResponse(course);
    }

    public async Task<IReadOnlyCollection<CourseResponse>> ListInstructorAsync(Guid instructorId, CancellationToken ct) =>
        (await db.Courses.AsNoTracking().Where(course => course.InstructorUserId == instructorId)
            .OrderByDescending(course => course.UpdatedAtUtc).ToListAsync(ct)).Select(ToResponse).ToArray();

    public async Task<IReadOnlyCollection<CourseResponse>> ListPendingAsync(CancellationToken ct) =>
        (await db.Courses.AsNoTracking().Where(course => course.Status == CourseStatus.PendingApproval)
            .OrderBy(course => course.CreatedAtUtc).ToListAsync(ct)).Select(ToResponse).ToArray();

    public async Task<CourseResponse> CreateAsync(Guid instructorId, CourseWriteRequest request, CancellationToken ct)
    {
        await EnsureActiveCategoryAsync(request.CategoryId, ct);
        var course = Course.Create(
            instructorId, request.CategoryId, request.Title, request.Summary, request.Description, request.ImageUrl);
        db.Courses.Add(course);
        AddAudit(instructorId, "CourseCreated", course);
        await db.SaveChangesAsync(ct);
        return ToResponse(course);
    }

    public async Task<CourseResponse> UpdateAsync(Guid instructorId, Guid courseId, CourseWriteRequest request, CancellationToken ct)
    {
        var course = await GetOwnedAsync(instructorId, courseId, ct);
        await EnsureActiveCategoryAsync(request.CategoryId, ct);
        course.Update(request.CategoryId, request.Title, request.Summary, request.Description, request.ImageUrl);
        AddAudit(instructorId, "CourseUpdated", course);
        await db.SaveChangesAsync(ct);
        return ToResponse(course);
    }

    public Task<CourseResponse> SubmitAsync(Guid instructorId, Guid courseId, CancellationToken ct) =>
        ChangeOwnedAsync(instructorId, courseId, "CourseSubmittedForReview", course => course.SubmitForReview(), ct);

    public Task<CourseResponse> ArchiveAsync(Guid instructorId, Guid courseId, CancellationToken ct) =>
        ChangeOwnedAsync(instructorId, courseId, "CourseArchived", course => course.Archive(), ct);

    public Task<CourseResponse> ApproveAsync(Guid adminId, Guid courseId, CancellationToken ct) =>
        ChangeAsync(adminId, courseId, "CourseApproved", course => course.Approve(), ct);

    public Task<CourseResponse> RejectAsync(Guid adminId, Guid courseId, string reason, CancellationToken ct) =>
        ChangeAsync(adminId, courseId, "CourseRejected", course => course.Reject(reason), ct);

    private async Task<CourseResponse> ChangeOwnedAsync(Guid ownerId, Guid courseId, string action, Action<Course> change, CancellationToken ct)
    {
        var course = await GetOwnedAsync(ownerId, courseId, ct);
        try { change(course); }
        catch (InvalidOperationException exception) { throw new BusinessRuleException(exception.Message); }
        AddAudit(ownerId, action, course);
        await db.SaveChangesAsync(ct);
        return ToResponse(course);
    }

    private async Task<CourseResponse> ChangeAsync(Guid actorId, Guid courseId, string action, Action<Course> change, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(item => item.Id == courseId, ct)
            ?? throw new NotFoundException("Course was not found.");
        try { change(course); }
        catch (InvalidOperationException exception) { throw new BusinessRuleException(exception.Message); }
        AddAudit(actorId, action, course);
        await db.SaveChangesAsync(ct);
        return ToResponse(course);
    }

    private async Task<Course> GetOwnedAsync(Guid instructorId, Guid courseId, CancellationToken ct) =>
        await db.Courses.SingleOrDefaultAsync(item => item.Id == courseId && item.InstructorUserId == instructorId, ct)
        ?? throw new NotFoundException("Course was not found.");

    private async Task EnsureActiveCategoryAsync(Guid categoryId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(category => category.Id == categoryId && category.IsActive, ct))
            throw new BusinessRuleException("An active category is required for a course.");
    }

    private void AddAudit(Guid actorId, string action, Course course) => db.AuditLogs.Add(AuditLog.Create(
        clock.GetUtcNow().UtcDateTime, action, nameof(Course), actorId, course.Id.ToString(),
        $"Course '{course.Title}' status: {course.Status}."));

    private static CourseResponse ToResponse(Course course) => new(
        course.Id, course.CategoryId, course.InstructorUserId, course.Title, course.Summary,
        course.Description, course.ImageUrl, course.Status.ToString(), course.RejectionReason,
        course.CreatedAtUtc, course.UpdatedAtUtc);
}
