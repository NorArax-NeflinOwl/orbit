using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Orbit.Api.HealthChecks;

/// <summary>
/// Flags a background service as unhealthy once it stops reporting heartbeats through
/// <see cref="HostedServiceHealthTracker"/>. Healthy by default when no background services are
/// registered yet, since there is nothing to check.
/// </summary>
public sealed class HostedServicesHealthCheck(HostedServiceHealthTracker tracker, IOptionsMonitor<HealthCheckSettings> settings) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var currentSettings = settings.CurrentValue.HostedServices;
        if (!currentSettings.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Check disabled by configuration."));
        }

        var heartbeats = tracker.GetLastHeartbeats();
        if (heartbeats.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("No background services registered."));
        }

        var now = DateTimeOffset.UtcNow;
        var staleServiceNames = heartbeats
            .Where(heartbeat => now - heartbeat.Value > TimeSpan.FromSeconds(currentSettings.StaleAfterSeconds))
            .Select(heartbeat => heartbeat.Key)
            .ToList();

        var data = heartbeats.ToDictionary(heartbeat => heartbeat.Key, heartbeat => (object)heartbeat.Value);

        return Task.FromResult(staleServiceNames.Count == 0
            ? HealthCheckResult.Healthy("All background services reported a recent heartbeat.", data)
            : HealthCheckResult.Unhealthy($"Stale heartbeat for: {string.Join(", ", staleServiceNames)}.", data: data));
    }
}
