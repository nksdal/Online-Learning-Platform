using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class CourseTests
{
    [Fact]
    public async Task Teacher_and_admin_can_complete_course_review_lifecycle()
    {
        using var factory = new AuthApiFactory();
        using var teacher = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var publicClient = factory.CreateClient();
        var categoryId = await CreateCategoryAsync(factory);
        var (_, teacherToken) = await CreateUserAsync(factory, "teacher@example.test", Roles.Teacher);
        var (_, adminToken) = await CreateUserAsync(factory, "admin@example.test", Roles.Admin);
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var request = new CourseWriteRequest(categoryId, "C# Basics", "Learn the basics", "Course details.");

        var create = await teacher.PostAsJsonAsync("/api/teacher/courses", request);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var course = await create.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(course);
        Assert.Equal("Draft", course.Status);
        Assert.Empty(await publicClient.GetFromJsonAsync<CourseResponse[]>("/api/courses") ?? []);
        var adminAllCourses = await admin.GetFromJsonAsync<CourseResponse[]>("/api/admin/courses");
        Assert.NotNull(adminAllCourses);
        Assert.Contains(adminAllCourses, item => item.Id == course.Id && item.Status == "Draft");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/courses/{course.Id}")).StatusCode);

        var submit = await teacher.PostAsync($"/api/teacher/courses/{course.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var pending = await admin.GetFromJsonAsync<CourseResponse[]>("/api/admin/courses/pending");
        Assert.NotNull(pending);
        Assert.Single(pending);

        var reject = await admin.PostAsJsonAsync($"/api/admin/courses/{course.Id}/reject", new CourseRejectionRequest("Add outcomes."));
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var rejected = await reject.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.Equal("Rejected", rejected!.Status);

        var update = await teacher.PutAsJsonAsync($"/api/teacher/courses/{course.Id}", request with { Title = "C# for Beginners" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var resubmit = await teacher.PostAsync($"/api/teacher/courses/{course.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
        var approve = await admin.PostAsync($"/api/admin/courses/{course.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Single(await publicClient.GetFromJsonAsync<CourseResponse[]>("/api/courses") ?? []);

        var archive = await teacher.PostAsync($"/api/teacher/courses/{course.Id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/courses/{course.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsync($"/api/teacher/courses/{course.Id}/submit", null)).StatusCode);
    }

    [Fact]
    public async Task Student_cannot_create_courses()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var (_, token) = await CreateUserAsync(factory, "student@example.test", Roles.Student);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/teacher/courses", new CourseWriteRequest(Guid.NewGuid(), "Title", "Summary", "Description"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<Guid> CreateCategoryAsync(AuthApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var category = Category.Create("Technology", null);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<(Guid Id, string Token)> CreateUserAsync(AuthApiFactory factory, string email, string role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new ApplicationRole(role))).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "StrongPassword17")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        var stamp = await users.GetSecurityStampAsync(user);
        var token = factory.Services.GetRequiredService<IAccessTokenService>().Create(user.Id, email, [role], securityStamp: stamp);
        return (user.Id, token.Value);
    }
}
