using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace OnlineLearningPlatform.IntegrationTests.TestSupport;

public sealed class BootstrapAdminApiFactory : AuthApiFactory
{
    public const string Email = "bootstrap-admin@example.test";
    public const string Password = "BootstrapAdminPass123";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Email"] = Email,
                ["BootstrapAdmin:Password"] = Password,
            }));
    }
}
