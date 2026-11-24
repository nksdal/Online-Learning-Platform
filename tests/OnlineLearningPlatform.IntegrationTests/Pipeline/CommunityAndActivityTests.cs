using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Domain.Catalog;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Catalog;
using OnlineLearningPlatform.Infrastructure.Persistence;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class CommunityAndActivityTests
{
    [Fact]
    public async Task Enrolled_students_can_discuss_and_course_owner_can_moderate_posts()
    {
        using var factory = new AuthApiFactory();
        using var teacher = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var firstStudent = factory.CreateClient();
        using var secondStudent = factory.CreateClient();
        var categoryId = await CreateCategoryAsync(factory, "Community");
        var (_, teacherToken) = await CreateUserAsync(factory, "community-teacher@example.test", Roles.Teacher);
        var (_, adminToken) = await CreateUserAsync(factory, "community-admin@example.test", Roles.Admin);
        var (_, firstStudentToken) = await CreateUserAsync(factory, "community-one@example.test", Roles.Student);
        var (_, secondStudentToken) = await CreateUserAsync(factory, "community-two@example.test", Roles.Student);
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        firstStudent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstStudentToken);
        secondStudent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondStudentToken);
        var course = await CreatePublishedCourseAsync(teacher, admin, categoryId, "Discussion course");

        Assert.Equal(HttpStatusCode.Created, (await firstStudent.PostAsync($"/api/courses/{course.Id}/enrollments", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await secondStudent.PostAsync($"/api/courses/{course.Id}/enrollments", null)).StatusCode);
        var outsider = factory.CreateClient();
        using (outsider)
        {
            var (_, token) = await CreateUserAsync(factory, "community-outsider@example.test", Roles.Student);
            outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.NotFound,
                (await outsider.GetAsync($"/api/courses/{course.Id}/discussions")).StatusCode);
        }

        var rootResponse = await firstStudent.PostAsJsonAsync($"/api/courses/{course.Id}/discussions",
            new DiscussionPostRequest("How should I structure the project?"));
        Assert.Equal(HttpStatusCode.Created, rootResponse.StatusCode);
        var root = await rootResponse.Content.ReadFromJsonAsync<DiscussionPostResponse>();
        Assert.NotNull(root);
        var replyResponse = await secondStudent.PostAsJsonAsync($"/api/discussions/{root.Id}/replies",
            new DiscussionPostRequest("Start with the API layer and add tests."));
        Assert.Equal(HttpStatusCode.Created, replyResponse.StatusCode);
        var reply = await replyResponse.Content.ReadFromJsonAsync<DiscussionPostResponse>();
        Assert.NotNull(reply);

        var teacherPosts = await teacher.GetFromJsonAsync<DiscussionPostsPageResponse>(
            $"/api/teacher/courses/{course.Id}/discussions");
        Assert.Equal(2, teacherPosts!.TotalCount);
        Assert.Equal(2, teacherPosts.Items.Count);
        Assert.Contains(teacherPosts.Items, post => post.Id == reply!.Id && post.ParentPostId == root.Id);
        var hidden = await teacher.PutAsJsonAsync($"/api/teacher/discussions/{root.Id}/moderation",
            new ModerateDiscussionRequest(true, "Contains off-topic material."));
        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        var visiblePosts = await firstStudent.GetFromJsonAsync<DiscussionPostsPageResponse>(
            $"/api/courses/{course.Id}/discussions");
        Assert.Single(visiblePosts!.Items);
        Assert.Equal(1, visiblePosts.TotalCount);

        var adminPosts = await admin.GetFromJsonAsync<DiscussionPostsPageResponse>(
            $"/api/admin/courses/{course.Id}/discussions");
        Assert.Equal(2, adminPosts!.Items.Count);
        Assert.Contains(adminPosts.Items, post => post.Id == root.Id && post.IsHidden);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(
            $"/api/admin/discussions/{root.Id}/moderation", new ModerateDiscussionRequest(false, null))).StatusCode);
        Assert.Equal(2, (await firstStudent.GetFromJsonAsync<DiscussionPostsPageResponse>(
            $"/api/courses/{course.Id}/discussions"))!.Items.Count);
    }

    [Fact]
    public async Task Discussion_lists_cap_pages_and_report_the_total_count()
    {
        using var factory = new AuthApiFactory();
        using var teacher = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var student = factory.CreateClient();
        var categoryId = await CreateCategoryAsync(factory, "Discussion pages");
        var (_, teacherToken) = await CreateUserAsync(factory, "discussion-page-teacher@example.test", Roles.Teacher);
        var (_, adminToken) = await CreateUserAsync(factory, "discussion-page-admin@example.test", Roles.Admin);
        var (studentId, studentToken) = await CreateUserAsync(factory, "discussion-page-student@example.test", Roles.Student);
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        student.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var course = await CreatePublishedCourseAsync(teacher, admin, categoryId, "Discussion paging course");
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsync($"/api/courses/{course.Id}/enrollments", null)).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.CourseDiscussionPosts.AddRange(Enumerable.Range(0, 52).Select(index =>
                CourseDiscussionPost.Create(course.Id, studentId, null, $"Post {index + 1}")));
            await db.SaveChangesAsync();
        }

        var first = await student.GetFromJsonAsync<DiscussionPostsPageResponse>(
            $"/api/courses/{course.Id}/discussions?page=1&pageSize=100");
        var second = await student.GetFromJsonAsync<DiscussionPostsPageResponse>(
            $"/api/courses/{course.Id}/discussions?page=2&pageSize=100");

        Assert.NotNull(first);
        Assert.Equal(50, first.PageSize);
        Assert.Equal(52, first.TotalCount);
        Assert.Equal(50, first.Items.Count);
        Assert.Equal(2, second!.Items.Count);
        Assert.Empty(first.Items.Select(post => post.Id).Intersect(second.Items.Select(post => post.Id)));
        Assert.Equal(first.Items.OrderByDescending(post => post.CreatedAtUtc).ThenByDescending(post => post.Id)
            .Select(post => post.Id), first.Items.Select(post => post.Id));
    }

    [Fact]
    public async Task Streak_uses_student_time_zone_and_recommendations_exclude_enrolled_courses()
    {
        using var factory = new AuthApiFactory();
        using var teacher = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var student = factory.CreateClient();
        var categoryId = await CreateCategoryAsync(factory, "Activity");
        var (_, teacherToken) = await CreateUserAsync(factory, "activity-teacher@example.test", Roles.Teacher);
        var (_, adminToken) = await CreateUserAsync(factory, "activity-admin@example.test", Roles.Admin);
        var (studentId, studentToken) = await CreateUserAsync(factory, "activity-student@example.test", Roles.Student, "Asia/Tbilisi");
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        student.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var enrolledCourse = await CreatePublishedCourseAsync(teacher, admin, categoryId, "Already enrolled");
        var recommendedCourse = await CreatePublishedCourseAsync(teacher, admin, categoryId, "Recommended course");
        var otherCategoryId = await CreateCategoryAsync(factory, "Other activity");
        var otherCategoryCourse = await CreatePublishedCourseAsync(teacher, admin, otherCategoryId, "Other category course");
        var (_, unstartedStudentToken) = await CreateUserAsync(factory, "activity-new-student@example.test", Roles.Student);
        using var unstartedStudent = factory.CreateClient();
        unstartedStudent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unstartedStudentToken);
        var popularResponse = await unstartedStudent.GetAsync("/api/users/me/recommendations?limit=1");
        Assert.True(popularResponse.IsSuccessStatusCode, await popularResponse.Content.ReadAsStringAsync());
        var popularRecommendations = await popularResponse.Content.ReadFromJsonAsync<RecommendedCourseResponse[]>();
        Assert.NotNull(popularRecommendations);
        Assert.Single(popularRecommendations);
        Assert.Equal("Popular with learners.", popularRecommendations[0].Reason);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsync($"/api/courses/{enrolledCourse.Id}/enrollments", null)).StatusCode);

        var timezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tbilisi");
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timezone).DateTime);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var day in new[] { localToday.AddDays(-2), localToday.AddDays(-1), localToday })
            {
                var localMidnight = day.ToDateTime(TimeOnly.MinValue);
                var completedAtUtc = TimeZoneInfo.ConvertTimeToUtc(localMidnight, timezone);
                db.LessonProgress.Add(LessonProgress.Create(Guid.NewGuid(), studentId, completedAtUtc));
            }
            await db.SaveChangesAsync();
        }

        var streak = await student.GetFromJsonAsync<StreakResponse>("/api/users/me/streak");
        Assert.NotNull(streak);
        Assert.Equal("Asia/Tbilisi", streak.TimeZoneId);
        Assert.Equal(3, streak.CurrentStreakDays);
        Assert.Equal(3, streak.LongestStreakDays);
        Assert.True(streak.LearnedToday);

        var recommendations = await student.GetFromJsonAsync<RecommendedCourseResponse[]>("/api/users/me/recommendations?limit=5");
        Assert.NotNull(recommendations);
        Assert.True(recommendations.Length <= 5);
        Assert.DoesNotContain(recommendations, item => item.CourseId == enrolledCourse.Id);
        Assert.Equal(recommendedCourse.Id, recommendations[0].CourseId);
        Assert.Contains(recommendations, item => item.CourseId == recommendedCourse.Id &&
            item.Reason == "Based on your enrolled course categories.");
        Assert.Contains(recommendations, item => item.CourseId == otherCategoryCourse.Id && item.Reason == "Popular with learners.");
    }

    [Fact]
    public async Task Deadline_reminder_sends_once_when_student_has_not_submitted()
    {
        using var factory = new AuthApiFactory();
        using var teacher = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var student = factory.CreateClient();
        var categoryId = await CreateCategoryAsync(factory, "Reminders");
        var (_, teacherToken) = await CreateUserAsync(factory, "reminder-teacher@example.test", Roles.Teacher);
        var (_, adminToken) = await CreateUserAsync(factory, "reminder-admin@example.test", Roles.Admin);
        var (_, studentToken) = await CreateUserAsync(factory, "reminder-student@example.test", Roles.Student);
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        student.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var course = await CreatePublishedCourseAsync(teacher, admin, categoryId, "Reminder course");
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsync($"/api/courses/{course.Id}/enrollments", null)).StatusCode);
        var due = DateTime.UtcNow.AddHours(1);
        var createAssignment = await teacher.PostAsJsonAsync($"/api/teacher/courses/{course.Id}/assignments",
            new AssignmentWriteRequest("Upcoming work", "Complete the exercise.", due));
        Assert.Equal(HttpStatusCode.Created, createAssignment.StatusCode);

        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IAssignmentDeadlineReminderProcessor>();
        var mail = factory.Services.GetRequiredService<CapturingEmailSender>();
        await processor.ProcessOnceAsync(CancellationToken.None);
        Assert.Single(mail.Messages);
        Assert.Contains("Upcoming work", mail.Messages.Single().Subject);
        await processor.ProcessOnceAsync(CancellationToken.None);
        Assert.Single(mail.Messages);
    }

    private static async Task<Guid> CreateCategoryAsync(AuthApiFactory factory, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var category = Category.Create(name, null);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<CourseResponse> CreatePublishedCourseAsync(
        HttpClient teacher, HttpClient admin, Guid categoryId, string title)
    {
        var response = await teacher.PostAsJsonAsync("/api/teacher/courses",
            new CourseWriteRequest(categoryId, title, "A short sample course.", "Course description."));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.NotNull(course);
        Assert.Equal(HttpStatusCode.OK, (await teacher.PostAsync($"/api/teacher/courses/{course.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/courses/{course.Id}/approve", null)).StatusCode);
        return course;
    }

    private static async Task<(Guid Id, string Token)> CreateUserAsync(
        AuthApiFactory factory, string email, string role, string timeZoneId = "UTC")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        if (!await roleManager.RoleExistsAsync(role))
            Assert.True((await roleManager.CreateAsync(new ApplicationRole(role))).Succeeded);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, TimeZoneId = timeZoneId };
        Assert.True((await userManager.CreateAsync(user, "StrongPassword17")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        var stamp = await userManager.GetSecurityStampAsync(user);
        var token = factory.Services.GetRequiredService<IAccessTokenService>()
            .Create(user.Id, email, [role], securityStamp: stamp);
        return (user.Id, token.Value);
    }
}
