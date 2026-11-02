using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OnlineLearningPlatform.IntegrationTests.TestSupport;

/// <summary>
/// Starts the real API in memory (real middleware, real DI, real controllers) with two changes:
/// the database connection points at nothing, and a "probe" controller is added so tests can make
/// the pipeline throw or validate on demand. Development environment by default.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected virtual string EnvironmentName => "Development";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Nothing listens on port 1, so connecting fails immediately and deterministically.
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=127.0.0.1,1;Database=Unreachable;User Id=unused;Password=unused;Connect Timeout=2;TrustServerCertificate=True",
                ["Authentication:Jwt:Issuer"] = "OnlineLearningPlatform.Tests",
                ["Authentication:Jwt:Audience"] = "OnlineLearningPlatform.Tests.Client",
                ["Authentication:Jwt:SigningKey"] = "integration-test-key-is-more-than-32-bytes",
                ["Authentication:Jwt:AccessTokenMinutes"] = "15",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
            services.AddScoped<IValidator<ProbeRequest>, ProbeRequestValidator>();
        });
    }
}

/// <summary>Same API, but running as the Production environment (used to prove Swagger is not exposed).</summary>
public sealed class ProductionApiFactory : ApiFactory
{
    protected override string EnvironmentName => "Production";
}
