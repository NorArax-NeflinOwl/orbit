using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Orbit.Api.HealthChecks;

/// <summary>
/// Probes every enabled entry under HealthChecks:ExternalServices:Services and reports unhealthy if
/// any of them fails to respond. The list is re-read from configuration on every run, so adding,
/// removing, or disabling a service takes effect without restarting the API.
/// </summary>
public sealed class ExternalServicesHealthCheck(ExternalServiceProber prober, IOptionsMonitor<HealthCheckSettings> settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var currentSettings = settings.CurrentValue.ExternalServices;
        if (!currentSettings.Enabled)
        {
            return HealthCheckResult.Healthy("Check disabled by configuration.");
        }

        var enabledEndpoints = currentSettings.Services.Where(service => service.Enabled).ToList();
        if (enabledEndpoints.Count == 0)
        {
            return HealthCheckResult.Healthy("No external services configured.");
        }

        var probeResults = await Task.WhenAll(enabledEndpoints.Select(endpoint => prober.ProbeAsync(endpoint, cancellationToken)));

        var data = probeResults.ToDictionary(
            result => result.Name,
            result => (object)new { result.Url, result.IsHealthy, result.Description, result.DurationMs });

        return probeResults.All(result => result.IsHealthy)
            ? HealthCheckResult.Healthy("All external services responded.", data)
            : HealthCheckResult.Unhealthy("One or more external services did not respond.", data: data);
    }
}
