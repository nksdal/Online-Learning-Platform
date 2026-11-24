using System.Net;
using OnlineLearningPlatform.IntegrationTests.TestSupport;

namespace OnlineLearningPlatform.IntegrationTests.Pipeline;

public sealed class RateLimitTests
{
    [Fact]
    public async Task Auth_endpoints_reject_requests_after_five_per_minute_from_one_ip()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var responses = new List<HttpResponseMessage>();

        try
        {
            for (var requestNumber = 0; requestNumber < 6; requestNumber++)
                responses.Add(await client.PostAsync("/api/auth/refresh", content: null));

            Assert.All(responses.Take(5), response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
            Assert.Equal((HttpStatusCode)429, responses[5].StatusCode);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }
}
