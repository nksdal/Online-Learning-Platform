using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class CategoryTests
{
    [Fact]
    public async Task Admin_can_create_update_deactivate_and_list_categories()
    {
        using var factory = new AuthApiFactory();
        using var adminClient = factory.CreateClient();
        using var publicClient = factory.CreateClient();
        var (adminId, token) = await CreateUserAsync(factory, "category-admin@example.test", Roles.Admin);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await adminClient.PostAsJsonAsync("/api/admin/categories", new CategoryWriteRequest("Backend", "Server-side courses"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var category = await create.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(category);
        Assert.Equal("Backend", category.Name);

        var publicCategories = await publicClient.GetFromJsonAsync<CategoryResponse[]>("/api/categories");
        Assert.NotNull(publicCategories);
        Assert.Single(publicCategories);

        var update = await adminClient.PutAsJsonAsync(
            $"/api/admin/categories/{category.Id}",
            new CategoryWriteRequest("Backend Engineering", "Updated description"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var deactivate = await adminClient.PutAsJsonAsync(
            $"/api/admin/categories/{category.Id}/status",
            new CategoryStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Empty(await publicClient.GetFromJsonAsync<CategoryResponse[]>("/api/categories") ?? []);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(dbContext.AuditLogs);
        Assert.Equal(3, audit.Count);
        Assert.All(audit, entry => Assert.Equal(adminId, entry.ActorUserId));
    }

    [Fact]
    public async Task Category_names_are_unique_without_case_sensitivity()
    {
        using var factory = new AuthApiFactory();
        using var adminClient = factory.CreateClient();
        var (_, token) = await CreateUserAsync(factory, "category-admin@example.test", Roles.Admin);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var first = await adminClient.PostAsJsonAsync("/api/admin/categories", new CategoryWriteRequest("Programming", null));
        var duplicate = await adminClient.PostAsJsonAsync("/api/admin/categories", new CategoryWriteRequest("programming", null));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_manage_categories()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var (_, token) = await CreateUserAsync(factory, "category-student@example.test", Roles.Student);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/categories", new CategoryWriteRequest("Design", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<(Guid Id, string Token)> CreateUserAsync(AuthApiFactory factory, string email, string role)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        if (!await roleManager.RoleExistsAsync(role))
        {
            Assert.True((await roleManager.CreateAsync(new ApplicationRole(role))).Succeeded);
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await userManager.CreateAsync(user, "StrongPassword17")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        var securityStamp = await userManager.GetSecurityStampAsync(user);
        var accessToken = factory.Services.GetRequiredService<IAccessTokenService>()
            .Create(user.Id, email, [role], securityStamp: securityStamp);
        return (user.Id, accessToken.Value);
    }
}
