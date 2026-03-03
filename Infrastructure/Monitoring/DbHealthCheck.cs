using Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Monitoring;

/// <summary>
/// Verifies the database is reachable.
/// Cloud Run (and Kubernetes) use /health/ready to decide whether to route traffic.
/// This check ensures the app won't serve requests if the DB is unavailable.
/// </summary>
public class DbHealthCheck(AppDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext checkContext,
        CancellationToken ct = default)
    {
        try
        {
            await context.Database.CanConnectAsync(ct);
            return HealthCheckResult.Healthy("Database is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
