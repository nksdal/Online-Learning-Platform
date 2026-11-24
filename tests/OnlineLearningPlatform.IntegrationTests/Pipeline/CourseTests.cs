using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task Admin_can_edit_pending_course_and_replace_its_cloudinary_cover()
    {
        using var factory = new CourseImageApiFactory();
        using var teacher = factory.CreateClient();
        using var admin = factory.CreateClient();
        var categoryId = await CreateCategoryAsync(factory);
        var (_, teacherToken) = await CreateUserAsync(factory, "teacher@example.test", Roles.Teacher);
        var (_, adminToken) = await CreateUserAsync(factory, "admin@example.test", Roles.Admin);
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var create = await teacher.PostAsJsonAsync("/api/teacher/courses",
            new CourseWriteRequest(categoryId, "Course before edit", "Original summary", "Original description."));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var original = await create.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(original);
        Assert.Equal(HttpStatusCode.OK,
            (await teacher.PostAsync($"/api/teacher/courses/{original.Id}/submit", null)).StatusCode);

        using var firstForm = CreateCourseEditForm(categoryId, "Course after edit", "Updated summary", "Updated details.");
        using (var image = new ByteArrayContent(PngHeader))
        {
            image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            firstForm.Add(image, "Image", "cover.png");
            var firstUpdate = await admin.PutAsync($"/api/admin/courses/{original.Id}", firstForm);
            Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);
            var saved = await firstUpdate.Content.ReadFromJsonAsync<CourseResponse>();
            Assert.NotNull(saved);
            Assert.Equal("Course after edit", saved.Title);
            Assert.Equal("Updated summary", saved.Summary);
            Assert.Equal("Updated details.", saved.Description);
            Assert.Equal("PendingApproval", saved.Status);
            Assert.Equal("https://res.cloudinary.test/courses/first.jpg", saved.ImageUrl);
        }

        using var secondForm = CreateCourseEditForm(categoryId, "Course with new cover", "Summary two", "Details two.");
        using (var image = new ByteArrayContent(PngHeader))
        {
            image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            secondForm.Add(image, "Image", "cover-two.png");
            var secondUpdate = await admin.PutAsync($"/api/admin/courses/{original.Id}", secondForm);
            Assert.Equal(HttpStatusCode.OK, secondUpdate.StatusCode);
        }

        Assert.Equal(2, factory.CourseImages.UploadCount);
        Assert.Equal(["nikladori/courses/test/first"], factory.CourseImages.DeletedPublicIds);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Courses.AsNoTracking().SingleAsync(course => course.Id == original.Id);
        Assert.Equal("Course with new cover", persisted.Title);
        Assert.Equal("PendingApproval", persisted.Status.ToString());
        Assert.Equal("nikladori/courses/test/second", persisted.CloudinaryImagePublicId);
    }

    [Fact]
    public async Task Admin_course_edit_rejects_non_image_upload_before_cloudinary()
    {
        using var factory = new CourseImageApiFactory();
        using var admin = factory.CreateClient();
        var (_, adminToken) = await CreateUserAsync(factory, "admin@example.test", Roles.Admin);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var form = CreateCourseEditForm(Guid.NewGuid(), "Title", "Summary", "Description.");
        using var image = new ByteArrayContent("not an image"u8.ToArray());
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(image, "Image", "fake.png");
        var response = await admin.PutAsync($"/api/admin/courses/{Guid.NewGuid()}", form);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(0, factory.CourseImages.UploadCount);
    }

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
        var request = new CourseWriteRequest(
            categoryId, "C# Basics", "Learn the basics", "Course details.",
            Duration: "8 weeks", Price: 19.99m, Difficulty: "Intermediate",
            Prerequisites: "Basic arithmetic");

        var create = await teacher.PostAsJsonAsync("/api/courses", request);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var course = await create.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(course);
        Assert.Equal("Draft", course.Status);
        Assert.Equal("8 weeks", course.Duration);
        Assert.Equal(19.99m, course.Price);
        Assert.Equal("Intermediate", course.Difficulty);
        Assert.Equal("Basic arithmetic", course.Prerequisites);
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

        var update = await teacher.PutAsJsonAsync($"/api/courses/{course.Id}", request with
        {
            Title = "C# for Beginners",
            Duration = "10 weeks",
            Price = 24.5m,
            Difficulty = "Advanced",
            Prerequisites = "Complete the first module",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updatedCourse = await update.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(updatedCourse);
        Assert.Equal("10 weeks", updatedCourse.Duration);
        Assert.Equal(24.5m, updatedCourse.Price);
        Assert.Equal("Advanced", updatedCourse.Difficulty);
        Assert.Equal("Complete the first module", updatedCourse.Prerequisites);
        var resubmit = await teacher.PostAsync($"/api/teacher/courses/{course.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
        var approve = await admin.PostAsync($"/api/admin/courses/{course.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Single(await publicClient.GetFromJsonAsync<CourseResponse[]>("/api/courses") ?? []);

        var archive = await teacher.DeleteAsync($"/api/courses/{course.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
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

    private static MultipartFormDataContent CreateCourseEditForm(Guid categoryId, string title, string summary, string description)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(categoryId.ToString()), "CategoryId");
        form.Add(new StringContent(title), "Title");
        form.Add(new StringContent(summary), "Summary");
        form.Add(new StringContent(description), "Description");
        return form;
    }

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private sealed class CourseImageApiFactory : AuthApiFactory
    {
        public FakeCourseImageStorage CourseImages => Services.GetRequiredService<FakeCourseImageStorage>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICourseImageStorage>();
                services.AddSingleton<FakeCourseImageStorage>();
                services.AddSingleton<ICourseImageStorage>(provider =>
                    provider.GetRequiredService<FakeCourseImageStorage>());
            });
        }
    }

    private sealed class FakeCourseImageStorage : ICourseImageStorage
    {
        public int UploadCount { get; private set; }
        public List<string> DeletedPublicIds { get; } = [];

        public Task<StoredCourseImage> UploadAsync(Guid courseId, string fileName, Stream content, CancellationToken cancellationToken)
        {
            UploadCount++;
            return Task.FromResult(UploadCount == 1
                ? new StoredCourseImage("https://res.cloudinary.test/courses/first.jpg", "nikladori/courses/test/first")
                : new StoredCourseImage("https://res.cloudinary.test/courses/second.jpg", "nikladori/courses/test/second"));
        }

        public Task DeleteAsync(string publicId, CancellationToken cancellationToken)
        {
            DeletedPublicIds.Add(publicId);
            return Task.CompletedTask;
        }
    }
}
