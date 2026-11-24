using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Health;

/// <summary>
/// Readiness check: can the application reach SQL Server and run a trivial query?
/// Uses a plain connection (not EF's retrying execution strategy) with a hard 5-second limit, so an
/// outage produces a fast "not ready" instead of a request that hangs. Failure details go to the log only;
/// the HTTP response says nothing about hosts, ports or exceptions.
/// </summary>
internal sealed class DatabaseHealthCheck(ApplicationDbContext dbContext, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await dbContext.Database.OpenConnectionAsync(timeout.Token);
            try
            {
                await using var command = dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(timeout.Token);
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database readiness check failed.");
            return HealthCheckResult.Unhealthy("Database is not reachable.");
        }
    }
}
