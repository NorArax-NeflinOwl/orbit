using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Orbit.Data;

namespace Orbit.Api.HealthChecks;

/// <summary>
/// Confirms the SQLite database is reachable by opening a connection. Reports healthy without
/// connecting while disabled via configuration, so a deployment that intentionally turns this check
/// off never fails because of it.
/// </summary>
public sealed class DatabaseHealthCheck(OrbitDbContext dbContext, IOptionsMonitor<HealthCheckSettings> settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!settings.CurrentValue.Database.Enabled)
        {
            return HealthCheckResult.Healthy("Check disabled by configuration.");
        }

        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("Database connection succeeded.")
            : HealthCheckResult.Unhealthy("Database connection failed.");
    }
}
