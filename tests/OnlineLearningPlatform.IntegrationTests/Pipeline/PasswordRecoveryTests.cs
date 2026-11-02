using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Authentication;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class PasswordRecoveryTests
{
    [Fact]
    public async Task Forgot_password_is_non_enumerating_and_sends_link_for_confirmed_active_account()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        const string email = "recovery@example.test";
        const string password = "StrongPassword17";
        await CreateConfirmedStudentAsync(factory, client, email, password);

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("missing@example.test"));

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        var resetEmail = Assert.Single(
            factory.Services.GetRequiredService<CapturingEmailSender>().Messages,
            message => message.Subject.Contains("Reset", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("reset-password?userId=", resetEmail.TextBody);
    }

    [Fact]
    public async Task Valid_reset_changes_password_and_revokes_existing_refresh_session()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });
        const string email = "recovery@example.test";
        const string oldPassword = "StrongPassword17";
        const string newPassword = "NewPassword284";
        await CreateConfirmedStudentAsync(factory, client, email, oldPassword);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, oldPassword));
        var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("olp_refresh=", StringComparison.Ordinal)).Split(';', 2)[0];
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var resetEmail = factory.Services.GetRequiredService<CapturingEmailSender>().Messages
            .Single(message => message.Subject.Contains("Reset", StringComparison.OrdinalIgnoreCase));
        var resetUrl = resetEmail.TextBody[(resetEmail.TextBody.LastIndexOf(' ') + 1)..].Trim();
        var query = QueryHelpers.ParseQuery(new Uri(resetUrl).Query);
        var resetRequest = new ResetPasswordRequest(
            Guid.Parse(query["userId"].ToString()),
            query["token"].ToString(),
            newPassword);

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("X-Refresh-Request", "1");
        refreshRequest.Headers.Add("Cookie", cookie);
        var refreshAfterReset = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterReset.StatusCode);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.False(await userManager.CheckPasswordAsync(user, oldPassword));
        Assert.True(await userManager.CheckPasswordAsync(user, newPassword));
    }

    [Fact]
    public async Task Reset_password_rejects_invalid_or_expired_token_with_generic_problem()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(Guid.NewGuid(), "invalid-token", "NewPassword284"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task CreateConfirmedStudentAsync(
        AuthApiFactory factory,
        HttpClient client,
        string email,
        string password)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            foreach (var role in Roles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    Assert.True((await roleManager.CreateAsync(new ApplicationRole(role))).Succeeded);
                }
            }
        }

        var registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Recovery", "Tester", password));
        Assert.Equal(HttpStatusCode.Accepted, registration.StatusCode);
        using var userScope = factory.Services.CreateScope();
        var userManager = userScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        user.EmailConfirmed = true;
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);
    }
}
