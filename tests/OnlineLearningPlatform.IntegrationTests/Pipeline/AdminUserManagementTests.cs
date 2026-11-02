using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class AdminUserManagementTests
{
    [Fact]
    public async Task Admin_can_view_own_profile_and_page_through_user_list()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var admin = await AddUserAsync(factory, "admin@example.test", Roles.Admin);
        await AddUserAsync(factory, "student@example.test", Roles.Student);
        SetBearer(client, admin.Token);

        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/users/me");
        var page = await client.GetFromJsonAsync<AdminUsersDto>("/api/admin/users?page=1&pageSize=1&search=example.test");

        Assert.NotNull(profile);
        Assert.Equal(admin.Id, profile.Id);
        Assert.Contains(Roles.Admin, profile.Roles);
        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Users);
    }

    [Fact]
    public async Task Role_change_is_audited_and_invalidates_the_target_users_old_access_token()
    {
        using var factory = new AuthApiFactory();
        using var adminClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var admin = await AddUserAsync(factory, "admin@example.test", Roles.Admin);
        var target = await AddUserAsync(factory, "student@example.test", Roles.Student);
        SetBearer(adminClient, admin.Token);
        SetBearer(targetClient, target.Token);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{target.Id}/role",
            new { role = Roles.Teacher });
        var staleTokenResponse = await targetClient.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, staleTokenResponse.StatusCode);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updatedUser = await userManager.FindByIdAsync(target.Id.ToString());
        Assert.NotNull(updatedUser);
        Assert.True(await userManager.IsInRoleAsync(updatedUser, Roles.Teacher));
        Assert.False(await userManager.IsInRoleAsync(updatedUser, Roles.Student));
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = await dbContext.AuditLogs.SingleAsync();
        Assert.Equal(admin.Id, audit.ActorUserId);
        Assert.Equal("UserRoleChanged", audit.Action);
        Assert.Equal(target.Id.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task Last_active_admin_cannot_be_demoted_or_deactivated()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var admin = await AddUserAsync(factory, "admin@example.test", Roles.Admin);
        SetBearer(client, admin.Token);

        var demote = await client.PutAsJsonAsync(
            $"/api/admin/users/{admin.Id}/role",
            new { role = Roles.Teacher });
        var deactivate = await client.PutAsJsonAsync(
            $"/api/admin/users/{admin.Id}/status",
            new { isActive = false });

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unchangedAdmin = await userManager.FindByIdAsync(admin.Id.ToString());
        Assert.NotNull(unchangedAdmin);
        Assert.True(unchangedAdmin.IsActive);
        Assert.True(await userManager.IsInRoleAsync(unchangedAdmin, Roles.Admin));
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await dbContext.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Deactivation_invalidates_the_target_access_token_and_is_audited()
    {
        using var factory = new AuthApiFactory();
        using var adminClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var admin = await AddUserAsync(factory, "admin@example.test", Roles.Admin);
        var target = await AddUserAsync(factory, "student@example.test", Roles.Student);
        SetBearer(adminClient, admin.Token);
        SetBearer(targetClient, target.Token);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{target.Id}/status",
            new { isActive = false });
        var oldTokenResponse = await targetClient.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenResponse.StatusCode);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var disabledUser = await userManager.FindByIdAsync(target.Id.ToString());
        Assert.NotNull(disabledUser);
        Assert.False(disabledUser.IsActive);
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = await dbContext.AuditLogs.SingleAsync();
        Assert.Equal("UserDeactivated", audit.Action);
    }

    [Fact]
    public async Task Non_admin_cannot_access_admin_user_management()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var student = await AddUserAsync(factory, "student@example.test", Roles.Student);
        SetBearer(client, student.Token);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<(Guid Id, string Token)> AddUserAsync(AuthApiFactory factory, string email, string role)
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
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
        };
        Assert.True((await userManager.CreateAsync(user, "StrongPassword17")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        var securityStamp = await userManager.GetSecurityStampAsync(user);
        var token = factory.Services.GetRequiredService<IAccessTokenService>()
            .Create(user.Id, email, [role], securityStamp: securityStamp);
        return (user.Id, token.Value);
    }

    private static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private sealed record ProfileDto(Guid Id, string Email, string FirstName, string LastName, string TimeZoneId, string[] Roles);

    private sealed record AdminUsersDto(int Page, int PageSize, int TotalCount, AdminUserDto[] Users);

    private sealed record AdminUserDto(Guid Id, string Email, string FirstName, string LastName, bool IsActive, bool MustChangePassword, string[] Roles);
}
