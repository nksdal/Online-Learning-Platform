using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class AssignmentTests
{
    [Fact]
    public async Task Student_attempts_are_preserved_and_teacher_can_grade_them()
    {
        using var factory = new AuthApiFactory();
        using var teacher = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var student = factory.CreateClient();
        var categoryId = await CreateCategoryAsync(factory);
        var (_, teacherToken) = await CreateUserAsync(factory, "assignment-teacher@example.test", Roles.Teacher);
        var (_, adminToken) = await CreateUserAsync(factory, "assignment-admin@example.test", Roles.Admin);
        var (_, studentToken) = await CreateUserAsync(factory, "assignment-student@example.test", Roles.Student);
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        student.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var createCourse = await teacher.PostAsJsonAsync("/api/teacher/courses",
            new CourseWriteRequest(categoryId, "Assignment Course", "Practice", "Course details."));
        var course = await createCourse.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(course);
        Assert.Equal(HttpStatusCode.OK, (await teacher.PostAsync($"/api/teacher/courses/{course.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/courses/{course.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsync($"/api/courses/{course.Id}/enrollments", null)).StatusCode);

        var assignmentRequest = new AssignmentWriteRequest(
            "API exercise", "Explain the request pipeline.", DateTime.UtcNow.AddDays(2), MaxAttempts: 2, MaxScore: 10);
        var createAssignment = await teacher.PostAsJsonAsync($"/api/teacher/courses/{course.Id}/assignments", assignmentRequest);
        Assert.Equal(HttpStatusCode.Created, createAssignment.StatusCode);
        var assignment = await createAssignment.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(assignment);
        Assert.Single(await student.GetFromJsonAsync<AssignmentResponse[]>($"/api/courses/{course.Id}/assignments") ?? []);

        var first = await student.PostAsJsonAsync($"/api/assignments/{assignment.Id}/submissions",
            new AssignmentSubmissionRequest("First attempt"));
        var second = await student.PostAsJsonAsync($"/api/assignments/{assignment.Id}/submissions",
            new AssignmentSubmissionRequest("Improved second attempt"));
        var third = await student.PostAsJsonAsync($"/api/assignments/{assignment.Id}/submissions",
            new AssignmentSubmissionRequest("Attempt beyond the limit"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);

        var myAttempts = await student.GetFromJsonAsync<AssignmentSubmissionResponse[]>(
            $"/api/assignments/{assignment.Id}/submissions/me");
        Assert.NotNull(myAttempts);
        Assert.Equal([1, 2], myAttempts.Select(item => item.AttemptNumber).ToArray());
        Assert.Equal(2, (await teacher.GetFromJsonAsync<AssignmentSubmissionResponse[]>(
            $"/api/teacher/assignments/{assignment.Id}/submissions"))!.Length);
        Assert.Equal(2, (await admin.GetFromJsonAsync<AssignmentSubmissionResponse[]>(
            $"/api/admin/assignments/{assignment.Id}/submissions"))!.Length);

        var grade = await teacher.PutAsJsonAsync($"/api/submissions/{myAttempts[0].Id}/grade",
            new GradeSubmissionRequest(8.5m, "Good explanation; add one more detail."));
        Assert.Equal(HttpStatusCode.OK, grade.StatusCode);
        var graded = await grade.Content.ReadFromJsonAsync<AssignmentSubmissionResponse>();
        Assert.Equal(8.5m, graded!.Score);
        Assert.Equal("Good explanation; add one more detail.", graded.Feedback);

        var invalidGrade = await teacher.PutAsJsonAsync($"/api/submissions/{myAttempts[1].Id}/grade",
            new GradeSubmissionRequest(11m, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidGrade.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.PutAsJsonAsync(
            $"/api/teacher/assignments/{assignment.Id}",
            assignmentRequest with { DueAtUtc = DateTime.UtcNow.AddDays(-1) })).StatusCode);
        var lateSubmission = await student.PostAsJsonAsync($"/api/assignments/{assignment.Id}/submissions",
            new AssignmentSubmissionRequest("Late attempt"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, lateSubmission.StatusCode);
    }

    private static async Task<Guid> CreateCategoryAsync(AuthApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var category = Category.Create("Assignments", null);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<(Guid Id, string Token)> CreateUserAsync(AuthApiFactory factory, string email, string role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        if (!await roleManager.RoleExistsAsync(role))
            Assert.True((await roleManager.CreateAsync(new ApplicationRole(role))).Succeeded);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await userManager.CreateAsync(user, "StrongPassword17")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        var stamp = await userManager.GetSecurityStampAsync(user);
        var token = factory.Services.GetRequiredService<IAccessTokenService>()
            .Create(user.Id, email, [role], securityStamp: stamp);
        return (user.Id, token.Value);
    }
}
