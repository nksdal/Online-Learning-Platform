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

public sealed class RegistrationTests
{
    [Fact]
    public async Task Registration_creates_an_unconfirmed_student_and_sends_a_verification_email()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await SeedRolesAsync(factory);

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration("learner@example.test"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var emails = factory.Services.GetRequiredService<CapturingEmailSender>().Messages;
        var email = Assert.Single(emails);
        Assert.Contains("verify-email?userId=", email.TextBody);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("learner@example.test");
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
        Assert.True(await userManager.IsInRoleAsync(user, Roles.Student));
    }

    [Fact]
    public async Task Verification_link_confirms_the_email_and_invalid_details_are_non_enumerating()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await SeedRolesAsync(factory);

        await client.PostAsJsonAsync("/api/auth/register", ValidRegistration("learner@example.test"));
        var email = Assert.Single(factory.Services.GetRequiredService<CapturingEmailSender>().Messages);
        var verificationUrl = email.TextBody[(email.TextBody.LastIndexOf(' ') + 1)..].Trim();
        var query = QueryHelpers.ParseQuery(new Uri(verificationUrl).Query);
        var userId = Guid.Parse(query["userId"].ToString());
        var token = query["token"].ToString();

        var verificationResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(userId, token));
        var invalidResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(Guid.NewGuid(), "invalid-token"));

        Assert.Equal(HttpStatusCode.OK, verificationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task Duplicate_registration_has_the_same_accepted_response_without_an_extra_email()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await SeedRolesAsync(factory);
        var request = ValidRegistration("learner@example.test");

        var first = await client.PostAsJsonAsync("/api/auth/register", request);
        var second = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Single(factory.Services.GetRequiredService<CapturingEmailSender>().Messages);
    }

    [Fact]
    public async Task Registration_rejects_a_password_that_does_not_meet_policy()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await SeedRolesAsync(factory);
        var request = ValidRegistration("learner@example.test") with { Password = "lowercaseonly1" };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Services.GetRequiredService<CapturingEmailSender>().Messages);
    }

    [Fact]
    public async Task Resend_verification_uses_the_same_generic_response_for_unknown_email()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/resend-verification",
            new ResendVerificationRequest("missing@example.test"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static RegisterRequest ValidRegistration(string email) =>
        new(email, "Alex", "Student", "StrongPassword17", "Asia/Tbilisi");

    private static async Task SeedRolesAsync(AuthApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
