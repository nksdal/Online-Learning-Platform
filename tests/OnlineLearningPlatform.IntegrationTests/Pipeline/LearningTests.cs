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

public sealed class LearningTests
{
    [Fact]
    public async Task Student_can_complete_reset_and_view_course_progress()
    {
        using var factory = new AuthApiFactory();
        using var teacher = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var student = factory.CreateClient();
        var categoryId = await CreateCategoryAsync(factory);
        var (_, teacherToken) = await CreateUserAsync(factory, "progress-teacher@example.test", Roles.Teacher);
        var (_, adminToken) = await CreateUserAsync(factory, "progress-admin@example.test", Roles.Admin);
        var (_, studentToken) = await CreateUserAsync(factory, "progress-student@example.test", Roles.Student);
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        student.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var createCourse = await teacher.PostAsJsonAsync("/api/teacher/courses",
            new CourseWriteRequest(categoryId, "Progress Course", "Track learning", "Course details."));
        var course = await createCourse.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(course);
        Assert.Equal(HttpStatusCode.OK, (await teacher.PostAsync($"/api/teacher/courses/{course.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/courses/{course.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsync($"/api/courses/{course.Id}/enrollments", null)).StatusCode);

        var first = await teacher.PostAsJsonAsync($"/api/teacher/courses/{course.Id}/lessons",
            new LessonWriteRequest("Lesson One", "First lesson content.", 1));
        var second = await teacher.PostAsJsonAsync($"/api/teacher/courses/{course.Id}/lessons",
            new LessonWriteRequest("Lesson Two", "Second lesson content.", 2));
        var firstLesson = await first.Content.ReadFromJsonAsync<LessonResponse>();
        Assert.NotNull(firstLesson);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var completion = await student.PutAsync($"/api/lessons/{firstLesson.Id}/progress", null);
        Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        Assert.True((await completion.Content.ReadFromJsonAsync<LessonCompletionResponse>())!.IsCompleted);
        Assert.Equal(HttpStatusCode.OK, (await student.PutAsync($"/api/lessons/{firstLesson.Id}/progress", null)).StatusCode);

        var courseProgress = await student.GetFromJsonAsync<CourseProgressResponse>($"/api/courses/{course.Id}/progress/me");
        Assert.NotNull(courseProgress);
        Assert.Equal(2, courseProgress.TotalLessons);
        Assert.Equal(1, courseProgress.CompletedLessons);
        Assert.Equal(50m, courseProgress.CompletionPercentage);

        Assert.Equal(HttpStatusCode.NoContent, (await student.DeleteAsync($"/api/lessons/{firstLesson.Id}/progress")).StatusCode);
        courseProgress = await student.GetFromJsonAsync<CourseProgressResponse>($"/api/courses/{course.Id}/progress/me");
        Assert.Equal(0, courseProgress!.CompletedLessons);
        Assert.Equal(0m, courseProgress.CompletionPercentage);

        Assert.Equal(HttpStatusCode.OK, (await teacher.PostAsync($"/api/teacher/courses/{course.Id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/api/courses/{course.Id}/progress/me")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await student.PutAsync($"/api/lessons/{firstLesson.Id}/progress", null)).StatusCode);
    }

    [Fact]
    public async Task Enrolled_student_keeps_lesson_access_after_course_is_archived()
    {
        using var factory = new AuthApiFactory();
        using var teacher = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var student = factory.CreateClient();
        using var otherStudent = factory.CreateClient();
        var categoryId = await CreateCategoryAsync(factory);
        var (_, teacherToken) = await CreateUserAsync(factory, "lessons-teacher@example.test", Roles.Teacher);
        var (_, adminToken) = await CreateUserAsync(factory, "lessons-admin@example.test", Roles.Admin);
        var (_, studentToken) = await CreateUserAsync(factory, "enrolled@example.test", Roles.Student);
        var (_, otherStudentToken) = await CreateUserAsync(factory, "not-enrolled@example.test", Roles.Student);
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        student.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        otherStudent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherStudentToken);

        var createdCourse = await teacher.PostAsJsonAsync("/api/teacher/courses",
            new CourseWriteRequest(categoryId, "Learning API", "Build an API", "A practical course."));
        Assert.Equal(HttpStatusCode.Created, createdCourse.StatusCode);
        var course = await createdCourse.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(course);
        Assert.Equal(HttpStatusCode.OK, (await teacher.PostAsync($"/api/teacher/courses/{course.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/courses/{course.Id}/approve", null)).StatusCode);

        var enrollment = await student.PostAsync($"/api/courses/{course.Id}/enrollments", null);
        Assert.Equal(HttpStatusCode.Created, enrollment.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsync($"/api/courses/{course.Id}/enrollments", null)).StatusCode);
        var myEnrollments = await student.GetFromJsonAsync<EnrollmentResponse[]>("/api/users/me/enrollments");
        Assert.NotNull(myEnrollments);
        Assert.Single(myEnrollments);
        Assert.Equal("Published", myEnrollments[0].CourseStatus);

        var createdLesson = await teacher.PostAsJsonAsync($"/api/teacher/courses/{course.Id}/lessons",
            new LessonWriteRequest("Welcome", "Start with the API overview.", 1));
        Assert.Equal(HttpStatusCode.Created, createdLesson.StatusCode);
        var lesson = await createdLesson.Content.ReadFromJsonAsync<LessonResponse>();
        Assert.NotNull(lesson);
        Assert.Single(await student.GetFromJsonAsync<LessonResponse[]>($"/api/courses/{course.Id}/lessons") ?? []);
        Assert.Single(await admin.GetFromJsonAsync<LessonResponse[]>($"/api/admin/courses/{course.Id}/lessons") ?? []);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherStudent.GetAsync($"/api/courses/{course.Id}/lessons")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await teacher.PostAsync($"/api/teacher/courses/{course.Id}/archive", null)).StatusCode);
        Assert.Single(await student.GetFromJsonAsync<LessonResponse[]>($"/api/courses/{course.Id}/lessons") ?? []);
        var archivedEnrollment = await student.GetFromJsonAsync<EnrollmentResponse[]>("/api/users/me/enrollments");
        Assert.NotNull(archivedEnrollment);
        Assert.Equal("Archived", Assert.Single(archivedEnrollment).CourseStatus);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await otherStudent.PostAsync($"/api/courses/{course.Id}/enrollments", null)).StatusCode);
    }

    private static async Task<Guid> CreateCategoryAsync(AuthApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var category = Category.Create("Learning", null);
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
