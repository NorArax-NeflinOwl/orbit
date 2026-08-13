using System.Collections.Concurrent;

namespace Orbit.Api.HealthChecks;

/// <summary>
/// Lets a background service report a heartbeat so <see cref="HostedServicesHealthCheck"/> can detect
/// one whose execution loop has crashed or gotten stuck. Inject this into any future
/// <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> and call <see cref="ReportHeartbeat"/>
/// on each iteration; no background services are registered yet, so the check stays healthy by default.
/// </summary>
public sealed class HostedServiceHealthTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> lastHeartbeatByServiceName = new();

    public void ReportHeartbeat(string serviceName) => lastHeartbeatByServiceName[serviceName] = DateTimeOffset.UtcNow;

    public IReadOnlyDictionary<string, DateTimeOffset> GetLastHeartbeats() => lastHeartbeatByServiceName;
}
