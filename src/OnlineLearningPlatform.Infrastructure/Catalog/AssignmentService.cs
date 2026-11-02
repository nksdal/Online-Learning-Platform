using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Domain.Auditing;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Catalog;

public sealed class AssignmentService(ApplicationDbContext db, TimeProvider clock) : IAssignmentService
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
    {
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
        var previousAttempts = await db.AssignmentSubmissions.CountAsync(
            item => item.AssignmentId == assignmentId && item.StudentUserId == studentId, ct);
        if (previousAttempts >= assignment.MaxAttempts)
            throw new ConflictException("You have used all attempts allowed for this assignment.");

        var submission = AssignmentSubmission.Create(assignmentId, studentId, previousAttempts + 1, request.Text, now);
        db.AssignmentSubmissions.Add(submission);
        db.AuditLogs.Add(AuditLog.Create(now, "AssignmentSubmitted", nameof(AssignmentSubmission), studentId,
            submission.Id.ToString(), $"Attempt {submission.AttemptNumber} submitted for '{assignment.Title}'."));
        await db.SaveChangesAsync(ct);
        return ToResponse(submission);
    }

    public async Task<IReadOnlyCollection<AssignmentSubmissionResponse>> ListMineAsync(Guid assignmentId, Guid studentId, CancellationToken ct)
    {
        await EnsureStudentCanReadAssignmentAsync(assignmentId, studentId, ct);
        var submissions = await db.AssignmentSubmissions.AsNoTracking()
            .Where(item => item.AssignmentId == assignmentId && item.StudentUserId == studentId)
            .OrderBy(item => item.AttemptNumber).ToListAsync(ct);
        return submissions.Select(ToResponse).ToArray();
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
        submission.SubmittedAtUtc, submission.Score, submission.Feedback, submission.GradedAtUtc, submission.GradedByUserId);
}
