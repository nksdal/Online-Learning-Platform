using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Domain.Auditing;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class AssignmentService(
    ApplicationDbContext db,
    TimeProvider clock,
    IMediaAssetStorage mediaStorage,
    ILogger<AssignmentService> logger) : IAssignmentService
{
    public async Task<IReadOnlyCollection<AssignmentResponse>> ListForStudentAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == courseId && (item.Status == CourseStatus.Published || item.Status == CourseStatus.Archived), ct);
        if (course is null || !await db.Enrollments.AnyAsync(
                item => item.CourseId == courseId && item.StudentUserId == studentId, ct))
            throw new NotFoundException("Course was not found.");
        return await ListAssignmentsAsync(courseId, ct);
    }

    public async Task<IReadOnlyCollection<AssignmentResponse>> ListForTeacherAsync(Guid courseId, Guid teacherId, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(item => item.Id == courseId && item.InstructorUserId == teacherId, ct))
            throw new NotFoundException("Course was not found.");
        return await ListAssignmentsAsync(courseId, ct);
    }

    public async Task<IReadOnlyCollection<AssignmentResponse>> ListForAdminAsync(Guid courseId, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(item => item.Id == courseId, ct))
            throw new NotFoundException("Course was not found.");
        return await ListAssignmentsAsync(courseId, ct);
    }

    public async Task<AssignmentResponse> CreateAsync(Guid courseId, Guid teacherId, AssignmentWriteRequest request, CancellationToken ct)
    {
        var course = await GetEditableCourseAsync(courseId, teacherId, ct);
        var assignment = CourseAssignment.Create(course.Id, request.Title, request.Instructions, request.DueAtUtc,
            request.MaxAttempts, request.MaxScore);
        db.Assignments.Add(assignment);
        AddAudit(teacherId, "AssignmentCreated", assignment);
        await db.SaveChangesAsync(ct);
        return ToResponse(assignment);
    }

    public async Task<AssignmentResponse> UpdateAsync(Guid assignmentId, Guid teacherId, AssignmentWriteRequest request, CancellationToken ct)
    {
        var assignment = await db.Assignments.SingleOrDefaultAsync(item => item.Id == assignmentId, ct)
            ?? throw new NotFoundException("Assignment was not found.");
        await GetEditableCourseAsync(assignment.CourseId, teacherId, ct);
        assignment.Update(request.Title, request.Instructions, request.DueAtUtc, request.MaxAttempts, request.MaxScore);
        AddAudit(teacherId, "AssignmentUpdated", assignment);
        await db.SaveChangesAsync(ct);
        return ToResponse(assignment);
    }

    public async Task<AssignmentSubmissionResponse> SubmitAsync(
        Guid assignmentId, Guid studentId, AssignmentSubmissionRequest request, CancellationToken ct)
        => await SubmitCoreAsync(assignmentId, studentId, request, attachment: null, fileName: null,
            contentType: null, fileSizeBytes: null, ct);

    public async Task<AssignmentSubmissionResponse> SubmitWithAttachmentAsync(
        Guid assignmentId,
        Guid studentId,
        AssignmentSubmissionRequest request,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        CancellationToken ct)
    {
        if (fileSizeBytes <= 0 || fileSizeBytes > 10 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "Submission attachments must be 10 MB or smaller.");

        // Check ownership, enrollment and deadlines before spending time on the Cloudinary upload.
        await EnsureSubmissionAllowedAsync(assignmentId, studentId, ct);
        var uploaded = await mediaStorage.UploadAsync(
            MediaAssetResourceType.Raw, $"nikladori/submissions/{assignmentId}", fileName, contentType, content, ct,
            privateDelivery: true);
        try
        {
            return await SubmitCoreAsync(assignmentId, studentId, request, uploaded, fileName,
                contentType, fileSizeBytes, ct);
        }
        catch
        {
            await TryDeleteUploadedAttachmentAsync(uploaded.PublicId, assignmentId);
            throw;
        }
    }

    private async Task<AssignmentSubmissionResponse> SubmitCoreAsync(
        Guid assignmentId,
        Guid studentId,
        AssignmentSubmissionRequest request,
        StoredMediaAsset? attachment,
        string? fileName,
        string? contentType,
        long? fileSizeBytes,
        CancellationToken ct)
    {
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var assignment = await db.Assignments.SingleOrDefaultAsync(item => item.Id == assignmentId, ct)
                    ?? throw new NotFoundException("Assignment was not found.");
                var course = await db.Courses.SingleOrDefaultAsync(item => item.Id == assignment.CourseId, ct)
                    ?? throw new NotFoundException("Course was not found.");
                if (!await db.Enrollments.AnyAsync(item => item.CourseId == course.Id && item.StudentUserId == studentId, ct))
                    throw new NotFoundException("Assignment was not found.");
                if (course.Status != CourseStatus.Published)
                    throw new BusinessRuleException("Submissions are accepted only while the course is published.");

                var now = clock.GetUtcNow().UtcDateTime;
                if (assignment.DueAtUtc.HasValue && now > assignment.DueAtUtc.Value)
                    throw new BusinessRuleException("The assignment deadline has passed.");
                var attempts = await db.AssignmentSubmissions.AsNoTracking()
                    .Where(item => item.AssignmentId == assignmentId && item.StudentUserId == studentId)
                    .OrderBy(item => item.AttemptNumber)
                    .Select(item => item.AttemptNumber)
                    .ToListAsync(ct);
                var nextAttemptNumber = attempts.Count == 0 ? 1 : attempts[^1] + 1;
                if (nextAttemptNumber > assignment.MaxAttempts)
                    throw new ConflictException("You have used all attempts allowed for this assignment.");

                var submission = AssignmentSubmission.Create(
                    assignmentId, studentId, nextAttemptNumber, request.Text, now,
                    attachment?.SecureUrl, attachment?.PublicId, fileName, contentType, fileSizeBytes);
                db.AssignmentSubmissions.Add(submission);
                db.AuditLogs.Add(AuditLog.Create(now, "AssignmentSubmitted", nameof(AssignmentSubmission), studentId,
                    submission.Id.ToString(), $"Attempt {submission.AttemptNumber} submitted for '{assignment.Title}'."));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return ToResponse(submission);
            });
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("You have used all attempts allowed for this assignment.");
        }
    }

    private async Task EnsureSubmissionAllowedAsync(Guid assignmentId, Guid studentId, CancellationToken ct)
    {
        var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == assignmentId, ct)
            ?? throw new NotFoundException("Assignment was not found.");
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == assignment.CourseId, ct)
            ?? throw new NotFoundException("Assignment was not found.");
        if (!await db.Enrollments.AnyAsync(item => item.CourseId == course.Id && item.StudentUserId == studentId, ct))
            throw new NotFoundException("Assignment was not found.");
        if (course.Status != CourseStatus.Published)
            throw new BusinessRuleException("Submissions are accepted only while the course is published.");
        if (assignment.DueAtUtc.HasValue && clock.GetUtcNow().UtcDateTime > assignment.DueAtUtc.Value)
            throw new BusinessRuleException("The assignment deadline has passed.");
        var attemptCount = await db.AssignmentSubmissions.CountAsync(
            item => item.AssignmentId == assignmentId && item.StudentUserId == studentId, ct);
        if (attemptCount >= assignment.MaxAttempts)
            throw new ConflictException("You have used all attempts allowed for this assignment.");
    }

    public async Task<SubmissionAttachmentDescriptor> GetAttachmentForStudentAsync(
        Guid submissionId, Guid studentId, CancellationToken ct)
    {
        var submission = await db.AssignmentSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == submissionId && item.StudentUserId == studentId, ct)
            ?? throw new NotFoundException("Submission attachment was not found.");
        await EnsureStudentCanReadAssignmentAsync(submission.AssignmentId, studentId, ct);
        return ToAttachmentDescriptor(submission);
    }

    public async Task<SubmissionAttachmentDescriptor> GetAttachmentForTeacherAsync(
        Guid submissionId, Guid teacherId, CancellationToken ct)
    {
        var submission = await db.AssignmentSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == submissionId, ct)
            ?? throw new NotFoundException("Submission attachment was not found.");
        var ownsCourse = await (
            from assignment in db.Assignments.AsNoTracking()
            join course in db.Courses.AsNoTracking() on assignment.CourseId equals course.Id
            where assignment.Id == submission.AssignmentId && course.InstructorUserId == teacherId
            select course.Id
        ).AnyAsync(ct);
        if (!ownsCourse)
            throw new NotFoundException("Submission attachment was not found.");
        return ToAttachmentDescriptor(submission);
    }

    public async Task<SubmissionAttachmentDescriptor> GetAttachmentForAdminAsync(Guid submissionId, CancellationToken ct)
    {
        var submission = await db.AssignmentSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == submissionId, ct)
            ?? throw new NotFoundException("Submission attachment was not found.");
        return ToAttachmentDescriptor(submission);
    }

    private static SubmissionAttachmentDescriptor ToAttachmentDescriptor(AssignmentSubmission submission)
    {
        if (string.IsNullOrWhiteSpace(submission.AttachmentPublicId) ||
            string.IsNullOrWhiteSpace(submission.AttachmentFileName) ||
            string.IsNullOrWhiteSpace(submission.AttachmentContentType) ||
            submission.AttachmentSizeBytes is null)
            throw new NotFoundException("Submission attachment was not found.");
        return new SubmissionAttachmentDescriptor(
            submission.AttachmentPublicId,
            submission.AttachmentFileName,
            submission.AttachmentContentType,
            submission.AttachmentSizeBytes.Value);
    }

    private async Task TryDeleteUploadedAttachmentAsync(string publicId, Guid assignmentId)
    {
        try
        {
            await mediaStorage.DeleteAsync(MediaAssetResourceType.Raw, publicId, CancellationToken.None,
                privateDelivery: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Could not clean up a newly uploaded assignment attachment after submission failed for {AssignmentId}.",
                assignmentId);
        }
    }

    public async Task<IReadOnlyCollection<AssignmentSubmissionResponse>> ListMineAsync(Guid assignmentId, Guid studentId, CancellationToken ct)
    {
        await EnsureStudentCanReadAssignmentAsync(assignmentId, studentId, ct);
        var submissions = await db.AssignmentSubmissions.AsNoTracking()
            .Where(item => item.AssignmentId == assignmentId && item.StudentUserId == studentId)
            .OrderBy(item => item.AttemptNumber).ToListAsync(ct);
        return submissions.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<AssignmentSubmissionResponse>>> ListMineForCourseAsync(
        Guid courseId, Guid studentId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == courseId && (item.Status == CourseStatus.Published || item.Status == CourseStatus.Archived), ct);
        if (course is null || !await db.Enrollments.AnyAsync(
                item => item.CourseId == courseId && item.StudentUserId == studentId, ct))
            throw new NotFoundException("Course was not found.");

        var rows = await (
            from assignment in db.Assignments.AsNoTracking().Where(item => item.CourseId == courseId)
            join submission in db.AssignmentSubmissions.AsNoTracking().Where(item => item.StudentUserId == studentId)
                on assignment.Id equals submission.AssignmentId into assignmentSubmissions
            from submission in assignmentSubmissions.DefaultIfEmpty()
            orderby assignment.Id, submission.AttemptNumber
            select new { AssignmentId = assignment.Id, Submission = submission }
        ).ToListAsync(ct);

        return rows.GroupBy(row => row.AssignmentId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<AssignmentSubmissionResponse>)group
                    .Where(row => row.Submission != null)
                    .Select(row => ToResponse(row.Submission!))
                    .ToArray());
    }

    public async Task<IReadOnlyCollection<AssignmentSubmissionResponse>> ListSubmissionsForTeacherAsync(
        Guid assignmentId, Guid teacherId, CancellationToken ct)
    {
        var assignment = await db.Assignments.SingleOrDefaultAsync(item => item.Id == assignmentId, ct)
            ?? throw new NotFoundException("Assignment was not found.");
        if (!await db.Courses.AnyAsync(item => item.Id == assignment.CourseId && item.InstructorUserId == teacherId, ct))
            throw new NotFoundException("Assignment was not found.");
        return await ListSubmissionsAsync(assignmentId, ct);
    }

    public async Task<IReadOnlyCollection<AssignmentSubmissionResponse>> ListSubmissionsForTeacherCourseAsync(
        Guid courseId, Guid teacherId, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(item => item.Id == courseId && item.InstructorUserId == teacherId, ct))
            throw new NotFoundException("Course was not found.");

        var submissions = await db.AssignmentSubmissions.AsNoTracking()
            .Where(item => db.Assignments.Any(assignment =>
                assignment.Id == item.AssignmentId && assignment.CourseId == courseId))
            .OrderBy(item => item.AssignmentId)
            .ThenBy(item => item.StudentUserId)
            .ThenBy(item => item.AttemptNumber)
            .ToListAsync(ct);
        return submissions.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<AssignmentSubmissionResponse>> ListSubmissionsForAdminAsync(Guid assignmentId, CancellationToken ct)
    {
        if (!await db.Assignments.AnyAsync(item => item.Id == assignmentId, ct))
            throw new NotFoundException("Assignment was not found.");
        return await ListSubmissionsAsync(assignmentId, ct);
    }

    public async Task<AssignmentSubmissionResponse> GradeAsync(
        Guid submissionId, Guid teacherId, GradeSubmissionRequest request, CancellationToken ct)
    {
        var submission = await db.AssignmentSubmissions.SingleOrDefaultAsync(item => item.Id == submissionId, ct)
            ?? throw new NotFoundException("Submission was not found.");
        var assignment = await db.Assignments.SingleAsync(item => item.Id == submission.AssignmentId, ct);
        await GetEditableCourseAsync(assignment.CourseId, teacherId, ct);
        if (request.Score > assignment.MaxScore)
            throw new BusinessRuleException($"Score cannot be greater than {assignment.MaxScore}.");
        var now = clock.GetUtcNow().UtcDateTime;
        submission.Grade(request.Score, request.Feedback, teacherId, now, assignment.MaxScore);
        db.AuditLogs.Add(AuditLog.Create(now, "SubmissionGraded", nameof(AssignmentSubmission), teacherId,
            submission.Id.ToString(), $"Submission graded with score {request.Score} out of {assignment.MaxScore}."));
        await db.SaveChangesAsync(ct);
        return ToResponse(submission);
    }

    private async Task<Course> GetEditableCourseAsync(Guid courseId, Guid teacherId, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(
            item => item.Id == courseId && item.InstructorUserId == teacherId, ct)
            ?? throw new NotFoundException("Course was not found.");
        if (course.Status is CourseStatus.PendingApproval or CourseStatus.Archived)
            throw new BusinessRuleException("Assignments cannot be changed while the course is pending review or archived.");
        return course;
    }

    private async Task EnsureStudentCanReadAssignmentAsync(Guid assignmentId, Guid studentId, CancellationToken ct)
    {
        var courseId = await db.Assignments.Where(item => item.Id == assignmentId)
            .Select(item => (Guid?)item.CourseId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Assignment was not found.");
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == courseId && (item.Status == CourseStatus.Published || item.Status == CourseStatus.Archived), ct);
        if (course is null || !await db.Enrollments.AnyAsync(
                item => item.CourseId == courseId && item.StudentUserId == studentId, ct))
            throw new NotFoundException("Assignment was not found.");
    }

    private async Task<IReadOnlyCollection<AssignmentResponse>> ListAssignmentsAsync(Guid courseId, CancellationToken ct) =>
        (await db.Assignments.AsNoTracking().Where(item => item.CourseId == courseId)
            .OrderBy(item => item.DueAtUtc).ThenBy(item => item.Title).ToListAsync(ct))
            .Select(ToResponse).ToArray();

    private async Task<IReadOnlyCollection<AssignmentSubmissionResponse>> ListSubmissionsAsync(Guid assignmentId, CancellationToken ct) =>
        (await db.AssignmentSubmissions.AsNoTracking().Where(item => item.AssignmentId == assignmentId)
            .OrderBy(item => item.StudentUserId).ThenBy(item => item.AttemptNumber).ToListAsync(ct))
            .Select(ToResponse).ToArray();

    private void AddAudit(Guid actorId, string action, CourseAssignment assignment) => db.AuditLogs.Add(AuditLog.Create(
        clock.GetUtcNow().UtcDateTime, action, nameof(CourseAssignment), actorId, assignment.Id.ToString(),
        $"Assignment '{assignment.Title}' in course {assignment.CourseId} was changed."));

    private static AssignmentResponse ToResponse(CourseAssignment assignment) => new(
        assignment.Id, assignment.CourseId, assignment.Title, assignment.Instructions, assignment.DueAtUtc,
        assignment.MaxAttempts, assignment.MaxScore, assignment.CreatedAtUtc, assignment.UpdatedAtUtc);

    private static AssignmentSubmissionResponse ToResponse(AssignmentSubmission submission) => new(
        submission.Id, submission.AssignmentId, submission.StudentUserId, submission.AttemptNumber, submission.Text,
        submission.SubmittedAtUtc, submission.Score, submission.Feedback, submission.GradedAtUtc, submission.GradedByUserId,
        string.IsNullOrWhiteSpace(submission.AttachmentPublicId) ? null : $"/api/submissions/{submission.Id}/attachment",
        submission.AttachmentFileName, submission.AttachmentContentType, submission.AttachmentSizeBytes);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 2601 or 2627 })
                return true;
        return false;
    }
}
