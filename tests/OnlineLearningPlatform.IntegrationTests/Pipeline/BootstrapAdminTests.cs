using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Authentication;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class BootstrapAdminTests
{
    [Fact]
    public async Task Bootstrap_admin_is_created_once_and_must_change_password_before_using_other_routes()
    {
        using var factory = new BootstrapAdminApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
            var admin = Assert.Single(admins);
            Assert.Equal(BootstrapAdminApiFactory.Email, admin.Email);
            Assert.True(admin.EmailConfirmed);
            Assert.True(admin.MustChangePassword);
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            BootstrapAdminApiFactory.Email,
            BootstrapAdminApiFactory.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(loginBody);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", loginBody.AccessToken);

        var blocked = await client.GetAsync("/test-support/protected");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("application/problem+json", blocked.Content.Headers.ContentType?.MediaType);

        var changePassword = await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest(
            BootstrapAdminApiFactory.Password,
            "ReplacementAdminPassword456"));
        Assert.Equal(HttpStatusCode.OK, changePassword.StatusCode);
        var changedSession = await changePassword.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(changedSession);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", changedSession.AccessToken);

        var allowed = await client.GetAsync("/test-support/protected");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var verifyUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updatedAdmin = await verifyUserManager.FindByEmailAsync(BootstrapAdminApiFactory.Email);
        Assert.NotNull(updatedAdmin);
        Assert.False(updatedAdmin.MustChangePassword);
        Assert.True(await verifyUserManager.CheckPasswordAsync(updatedAdmin, "ReplacementAdminPassword456"));
    }
}
