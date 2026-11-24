using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearningPlatform.Application.Authentication;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class RefreshTokenTests
{
    [Fact]
    public async Task Login_sets_http_only_cookie_and_refresh_rotates_token_with_reuse_detection()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        const string email = "refresh@example.test";
        const string password = "StrongPassword17";
        await CreateConfirmedStudentAsync(factory, client, email, password);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.DoesNotContain("refreshToken", loginBody, StringComparison.OrdinalIgnoreCase);
        var oldCookieHeader = GetCookieHeader(login);
        Assert.Contains("httponly", oldCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", oldCookieHeader, StringComparison.OrdinalIgnoreCase);
        var oldToken = GetCookieValue(oldCookieHeader);

        var refresh = await client.SendAsync(CreateRefreshRequest(oldCookieHeader));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var newCookieHeader = GetCookieHeader(refresh);
        var newToken = GetCookieValue(newCookieHeader);
        Assert.NotEqual(oldToken, newToken);

        var oldTokenReuse = await client.SendAsync(CreateRefreshRequest(oldCookieHeader));
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenReuse.StatusCode);
        var familyRevoked = await client.SendAsync(CreateRefreshRequest(newCookieHeader));
        Assert.Equal(HttpStatusCode.Unauthorized, familyRevoked.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await dbContext.RefreshTokens.ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.All(stored, token => Assert.DoesNotContain(token.TokenHash, new[] { oldToken, newToken }));
        Assert.All(stored, token => Assert.True(token.IsRevoked));
    }

    [Fact]
    public async Task Refresh_requires_custom_header_and_rejects_missing_cookie()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var missingHeader = await client.PostAsync("/api/auth/refresh", content: null);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("X-Refresh-Request", "1");
        var missingCookie = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, missingHeader.StatusCode);
        Assert.Equal("application/problem+json", missingHeader.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Unauthorized, missingCookie.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_current_session_and_clears_cookie()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        const string email = "logout@example.test";
        const string password = "StrongPassword17";
        await CreateConfirmedStudentAsync(factory, client, email, password);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var cookie = GetCookieHeader(login);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("X-Refresh-Request", "1");
        logout.Headers.Add("Cookie", cookie);
        var logoutResponse = await client.SendAsync(logout);
        var refreshAfterLogout = await client.SendAsync(CreateRefreshRequest(cookie));

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Contains(logoutResponse.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_all_requires_bearer_token_and_revokes_the_refresh_session()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        const string email = "logout-all@example.test";
        const string password = "StrongPassword17";
        await CreateConfirmedStudentAsync(factory, client, email, password);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var cookie = GetCookieHeader(login);
        var access = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(access);

        using var logoutAll = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout-all");
        logoutAll.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
        logoutAll.Headers.Add("X-Refresh-Request", "1");
        logoutAll.Headers.Add("Cookie", cookie);
        var logoutResponse = await client.SendAsync(logoutAll);
        var refreshAfterLogout = await client.SendAsync(CreateRefreshRequest(cookie));

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    private static HttpRequestMessage CreateRefreshRequest(string cookieHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("X-Refresh-Request", "1");
        request.Headers.Add("Cookie", cookieHeader);
        return request;
    }

    private static string GetCookieHeader(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("olp_refresh=", StringComparison.Ordinal));

    private static string GetCookieValue(string cookieHeader) => cookieHeader.Split(';', 2)[0]["olp_refresh=".Length..];

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
            new RegisterRequest(email, "Refresh", "Tester", password));
        Assert.Equal(HttpStatusCode.Accepted, registration.StatusCode);
        using var userScope = factory.Services.CreateScope();
        var userManager = userScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        user.EmailConfirmed = true;
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);
    }
}
