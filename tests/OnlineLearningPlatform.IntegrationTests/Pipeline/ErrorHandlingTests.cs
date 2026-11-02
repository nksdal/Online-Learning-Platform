using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public class ErrorHandlingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient CreateClient()
    {
        var client = factory.CreateClient();
        // Like a browser/Angular client: we ask for JSON.
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    [Theory]
    [InlineData("not-found", HttpStatusCode.NotFound)]
    [InlineData("forbidden", HttpStatusCode.Forbidden)]
    [InlineData("conflict", HttpStatusCode.Conflict)]
    [InlineData("business-rule", HttpStatusCode.UnprocessableEntity)]
    public async Task Application_exceptions_become_problem_details_with_the_matching_status(
        string probe,
        HttpStatusCode expectedStatus)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync($"/test-support/{probe}");

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expectedStatus, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("detail").GetString()));
        Assert.True(body.RootElement.TryGetProperty("traceId", out _), "Problem details must carry a traceId for support.");
    }

    [Fact]
    public async Task Unexpected_exceptions_return_a_generic_500_and_leak_nothing()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/test-support/crash");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // The exception message contained a fake secret; none of it may reach the client.
        Assert.DoesNotContain("do-not-leak", raw);
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain("OnlineLearningPlatform.", raw); // would appear in any stack trace
    }

    [Fact]
    public async Task Unknown_routes_return_a_404_problem_details_document()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_requests_return_400_with_camelCase_field_errors()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/test-support/validate", new { email = "not-an-email", age = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _), "Expected an 'email' error (camelCase key).");
        Assert.True(errors.TryGetProperty("age", out _), "Expected an 'age' error (camelCase key).");
        Assert.False(errors.TryGetProperty("Email", out _), "Keys must not be PascalCase.");
    }

    [Fact]
    public async Task Valid_requests_pass_through_the_validation_filter()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/test-support/validate", new { email = "student@example.com", age = 20 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
