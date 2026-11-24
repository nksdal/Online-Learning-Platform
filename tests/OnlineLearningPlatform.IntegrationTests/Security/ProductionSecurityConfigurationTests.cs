using Microsoft.Extensions.Configuration;
using OnlineLearningPlatform.Api.Security;

namespace OnlineLearningPlatform.IntegrationTests.Security;

public sealed class ProductionSecurityConfigurationTests
{
    [Fact]
    public void Validate_AcceptsProductionSettingsWithStrictDatabaseAndOriginConfiguration()
    {
        var configuration = CreateConfiguration();

        ProductionSecurityConfiguration.Validate(configuration);
    }

    [Fact]
    public void Validate_RejectsSwaggerInProduction()
    {
        var configuration = CreateConfiguration();
        configuration["Swagger:Enabled"] = "true";

        Assert.Contains("Swagger must be disabled.",
            Assert.Throws<InvalidOperationException>(() => ProductionSecurityConfiguration.Validate(configuration)).Message);
    }

    [Theory]
    [InlineData("User ID=sa;Password=placeholder-only;Encrypt=True;TrustServerCertificate=False")]
    [InlineData("User ID=olp_app;Password=placeholder-only;Encrypt=False;TrustServerCertificate=False")]
    [InlineData("User ID=olp_app;Password=placeholder-only;Encrypt=True;TrustServerCertificate=True")]
    public void Validate_RejectsUnsafeDatabaseConnections(string connectionString)
    {
        var configuration = CreateConfiguration();
        configuration["ConnectionStrings:DefaultConnection"] =
            "Server=db.nikladori.ge;Database=OnlineLearningPlatform;" + connectionString;

        Assert.Throws<InvalidOperationException>(() => ProductionSecurityConfiguration.Validate(configuration));
    }

    [Fact]
    public void Validate_RejectsNonHttpsCorsOrigins()
    {
        var configuration = CreateConfiguration();
        configuration["Cors:AllowedOrigins:0"] = "http://app.nikladori.ge";

        Assert.Contains("Cors:AllowedOrigins must contain only exact HTTPS application origins.",
            Assert.Throws<InvalidOperationException>(() => ProductionSecurityConfiguration.Validate(configuration)).Message);
    }

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "false",
            ["DemoCatalog:Enabled"] = "false",
            ["AllowedHosts"] = "api.nikladori.ge",
            ["Cors:AllowedOrigins:0"] = "https://app.nikladori.ge",
            ["Email:AppUrl"] = "https://app.nikladori.ge",
            ["ConnectionStrings:DefaultConnection"] =
                "Server=db.nikladori.ge;Database=OnlineLearningPlatform;User ID=olp_app;" +
                "Password=placeholder-only;Encrypt=True;TrustServerCertificate=False",
        })
        .Build();
}
