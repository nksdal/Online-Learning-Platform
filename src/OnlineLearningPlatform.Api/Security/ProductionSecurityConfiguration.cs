using System.Data.Common;
using System.Globalization;

namespace OnlineLearningPlatform.Api.Security;

/// <summary>
/// Fails closed when the app is started with production settings that still contain development defaults.
/// </summary>
public static class ProductionSecurityConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        var failures = new List<string>();

        if (configuration.GetValue<bool>("Swagger:Enabled"))
            failures.Add("Swagger must be disabled.");

        if (configuration.GetValue<bool>("DemoCatalog:Enabled"))
            failures.Add("Demo catalog seeding must be disabled.");

        var hosts = (configuration["AllowedHosts"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length == 0 || hosts.Any(IsDevelopmentHost))
            failures.Add("AllowedHosts must list the real API host names and must not contain development or wildcard hosts.");

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin => !IsHttpsOrigin(origin)))
            failures.Add("Cors:AllowedOrigins must contain only exact HTTPS application origins.");

        var appUrl = configuration["Email:AppUrl"];
        if (!Uri.TryCreate(appUrl, UriKind.Absolute, out var appUri) ||
            appUri.Scheme != Uri.UriSchemeHttps || IsDevelopmentHost(appUri.Host))
        {
            failures.Add("Email:AppUrl must be the real HTTPS application URL.");
        }

        ValidateDatabaseConnection(configuration.GetConnectionString("DefaultConnection"), failures);

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Production security configuration is invalid: " + string.Join(" ", failures));
        }
    }

    private static void ValidateDatabaseConnection(string? connectionString, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            failures.Add("A production database connection string is required.");
            return;
        }

        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            failures.Add("The production database connection string is malformed.");
            return;
        }

        string Value(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (builder.TryGetValue(key, out var value))
                    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return string.Empty;
        }

        var user = Value("User ID", "UID");
        if (string.IsNullOrWhiteSpace(user) || user.Equals("sa", StringComparison.OrdinalIgnoreCase))
            failures.Add("The production API database connection must use a dedicated non-sa account.");

        var database = Value("Initial Catalog", "Database");
        if (string.IsNullOrWhiteSpace(database) || database.Equals("master", StringComparison.OrdinalIgnoreCase))
            failures.Add("The production API database connection must target the application database.");

        var encryption = Value("Encrypt");
        if (!encryption.Equals("true", StringComparison.OrdinalIgnoreCase) &&
            !encryption.Equals("mandatory", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("The production database connection must explicitly enable encryption.");
        }

        var trustServerCertificate = Value("TrustServerCertificate");
        if (!trustServerCertificate.Equals("false", StringComparison.OrdinalIgnoreCase))
            failures.Add("The production database connection must validate the server certificate.");
    }

    private static bool IsHttpsOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        uri.AbsolutePath == "/" &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) &&
        !IsDevelopmentHost(uri.Host);

    private static bool IsDevelopmentHost(string host) =>
        host.Contains('*') ||
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("[::1]", StringComparison.OrdinalIgnoreCase) ||
        host.Contains("example.", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".test", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);
}
