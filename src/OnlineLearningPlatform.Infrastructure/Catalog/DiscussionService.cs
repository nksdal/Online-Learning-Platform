using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Domain.Auditing;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class DiscussionService(ApplicationDbContext db, TimeProvider clock) : IDiscussionService
{
    public async Task<IReadOnlyCollection<DiscussionPostResponse>> ListForStudentAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        await EnsureStudentCanReadAsync(courseId, studentId, ct);
        var posts = await db.CourseDiscussionPosts.AsNoTracking()
            .Where(post => post.CourseId == courseId && !post.IsHidden)
            .OrderBy(post => post.CreatedAtUtc).ToListAsync(ct);
        return posts.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<DiscussionPostResponse>> ListForTeacherAsync(Guid courseId, Guid teacherId, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(course => course.Id == courseId && course.InstructorUserId == teacherId, ct))
            throw new NotFoundException("Course was not found.");
        return await ListAllPostsAsync(courseId, ct);
    }

    public async Task<IReadOnlyCollection<DiscussionPostResponse>> ListForAdminAsync(Guid courseId, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(course => course.Id == courseId, ct))
            throw new NotFoundException("Course was not found.");
        return await ListAllPostsAsync(courseId, ct);
    }

    public async Task<DiscussionPostResponse> CreateAsync(Guid courseId, Guid studentId, DiscussionPostRequest request, CancellationToken ct)
    {
        await EnsureStudentCanPostAsync(courseId, studentId, ct);
        var post = CourseDiscussionPost.Create(courseId, studentId, null, request.Body);
        db.CourseDiscussionPosts.Add(post);
        await db.SaveChangesAsync(ct);
        return ToResponse(post);
    }

    public async Task<DiscussionPostResponse> ReplyAsync(Guid parentPostId, Guid studentId, DiscussionPostRequest request, CancellationToken ct)
    {
        var parent = await db.CourseDiscussionPosts.SingleOrDefaultAsync(post => post.Id == parentPostId, ct)
            ?? throw new NotFoundException("Discussion post was not found.");
        await EnsureStudentCanPostAsync(parent.CourseId, studentId, ct);
        if (parent.IsHidden) throw new NotFoundException("Discussion post was not found.");
        var reply = CourseDiscussionPost.Create(parent.CourseId, studentId, parent.Id, request.Body);
        db.CourseDiscussionPosts.Add(reply);
        await db.SaveChangesAsync(ct);
        return ToResponse(reply);
    }

    public async Task<DiscussionPostResponse> ModerateAsync(
        Guid postId, Guid actorId, bool isAdmin, ModerateDiscussionRequest request, CancellationToken ct)
    {
        var post = await db.CourseDiscussionPosts.SingleOrDefaultAsync(item => item.Id == postId, ct)
            ?? throw new NotFoundException("Discussion post was not found.");
        if (!isAdmin && !await db.Courses.AnyAsync(
                course => course.Id == post.CourseId && course.InstructorUserId == actorId, ct))
            throw new NotFoundException("Discussion post was not found.");
        post.SetModeration(request.IsHidden, request.Reason, actorId, clock.GetUtcNow().UtcDateTime);
        db.AuditLogs.Add(AuditLog.Create(clock.GetUtcNow().UtcDateTime,
            request.IsHidden ? "DiscussionPostHidden" : "DiscussionPostRestored",
            nameof(CourseDiscussionPost), actorId, post.Id.ToString(), request.Reason));
        await db.SaveChangesAsync(ct);
        return ToResponse(post);
    }

    private async Task EnsureStudentCanReadAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == courseId && (item.Status == CourseStatus.Published || item.Status == CourseStatus.Archived), ct);
        if (course is null || !await db.Enrollments.AnyAsync(
                enrollment => enrollment.CourseId == courseId && enrollment.StudentUserId == studentId, ct))
            throw new NotFoundException("Course was not found.");
    }

    private async Task EnsureStudentCanPostAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(course => course.Id == courseId && course.Status == CourseStatus.Published, ct) ||
            !await db.Enrollments.AnyAsync(enrollment => enrollment.CourseId == courseId && enrollment.StudentUserId == studentId, ct))
            throw new NotFoundException("Course was not found.");
    }

    private async Task<IReadOnlyCollection<DiscussionPostResponse>> ListAllPostsAsync(Guid courseId, CancellationToken ct) =>
        (await db.CourseDiscussionPosts.AsNoTracking().Where(post => post.CourseId == courseId)
            .OrderBy(post => post.CreatedAtUtc).ToListAsync(ct)).Select(ToResponse).ToArray();

    private static DiscussionPostResponse ToResponse(CourseDiscussionPost post) => new(
        post.Id, post.CourseId, post.AuthorUserId, post.ParentPostId, post.Body, post.IsHidden,
        post.ModerationReason, post.CreatedAtUtc, post.UpdatedAtUtc);
}
