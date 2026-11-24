using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Authentication;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class LoginTests
{
    [Fact]
    public async Task Confirmed_active_student_can_login_and_use_the_access_token()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await SeedRolesAsync(factory);
        const string email = "learner@example.test";
        const string password = "StrongPassword17";

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Alex", "Student", password));
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            user.EmailConfirmed = true;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(token);
        Assert.True(token.ExpiresAtUtc > DateTime.UtcNow);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var protectedResponse = await client.GetAsync("/test-support/protected");
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_unconfirmed_users_and_wrong_passwords_with_the_same_response()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await SeedRolesAsync(factory);
        const string email = "learner@example.test";
        const string password = "StrongPassword17";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Alex", "Student", password));

        var unconfirmed = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            user.EmailConfirmed = true;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword17"));

        Assert.Equal(HttpStatusCode.Unauthorized, unconfirmed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal("application/problem+json", unconfirmed.Content.Headers.ContentType?.MediaType);
        using var unconfirmedProblem = JsonDocument.Parse(await unconfirmed.Content.ReadAsStringAsync());
        using var wrongPasswordProblem = JsonDocument.Parse(await wrongPassword.Content.ReadAsStringAsync());
        Assert.Equal(unconfirmedProblem.RootElement.GetProperty("title").GetString(),
            wrongPasswordProblem.RootElement.GetProperty("title").GetString());
        Assert.Equal(unconfirmedProblem.RootElement.GetProperty("status").GetInt32(),
            wrongPasswordProblem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Inactive_user_cannot_login()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await SeedRolesAsync(factory);
        const string email = "learner@example.test";
        const string password = "StrongPassword17";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Alex", "Student", password));
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            user.IsActive = false;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
            user.EmailConfirmed = true;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task SeedRolesAsync(AuthApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole(role));
                Assert.True(result.Succeeded);
            }
        }
    }
}
