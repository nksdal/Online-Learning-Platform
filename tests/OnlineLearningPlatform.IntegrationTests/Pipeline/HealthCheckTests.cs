using System.Net;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public class HealthCheckTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Liveness_is_healthy_even_when_the_database_is_down()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_reports_503_when_the_database_is_unreachable_without_leaking_details()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", raw);
        Assert.DoesNotContain("127.0.0.1", raw);
        Assert.DoesNotContain("Unreachable", raw);
        Assert.DoesNotContain("Sql", raw);
    }
}
