using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task Protected_endpoint_rejects_anonymous_request()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/test-support/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_accepts_a_valid_signed_access_token()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var (userId, email, securityStamp) = await CreateUserAsync(factory);
        var tokenService = factory.Services.GetRequiredService<IAccessTokenService>();
        var token = tokenService.Create(userId, email, [Roles.Student], securityStamp: securityStamp);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        var response = await client.GetAsync("/test-support/protected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Role_policy_rejects_a_valid_token_without_the_required_role()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var (userId, email, securityStamp) = await CreateUserAsync(factory);
        var tokenService = factory.Services.GetRequiredService<IAccessTokenService>();
        var token = tokenService.Create(userId, email, [Roles.Student], securityStamp: securityStamp);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        var response = await client.GetAsync("/test-support/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<(Guid UserId, string Email, string SecurityStamp)> CreateUserAsync(AuthApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string email = "pipeline-user@example.test";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, "StrongPassword17");
        Assert.True(result.Succeeded);
        return (user.Id, email, await userManager.GetSecurityStampAsync(user));
    }
}
