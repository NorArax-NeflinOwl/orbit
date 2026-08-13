using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orbit.Api.HealthChecks;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.HealthChecks;

public sealed class HostedServicesHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_returns_healthy_without_checking_heartbeats_when_disabled()
    {
        var tracker = new HostedServiceHealthTracker();
        tracker.ReportHeartbeat("note-sync-worker");
        var settings = new HealthCheckSettings { HostedServices = new HostedServicesHealthCheckSettings { Enabled = false } };
        var healthCheck = new HostedServicesHealthCheck(tracker, new TestOptionsMonitor<HealthCheckSettings>(settings));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Check disabled by configuration.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_no_services_are_registered()
    {
        var tracker = new HostedServiceHealthTracker();
        var settings = new HealthCheckSettings { HostedServices = new HostedServicesHealthCheckSettings { Enabled = true } };
        var healthCheck = new HostedServicesHealthCheck(tracker, new TestOptionsMonitor<HealthCheckSettings>(settings));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("No background services registered.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_all_heartbeats_are_recent()
    {
        var tracker = new HostedServiceHealthTracker();
        tracker.ReportHeartbeat("note-sync-worker");
        var settings = new HealthCheckSettings
        {
            HostedServices = new HostedServicesHealthCheckSettings { Enabled = true, StaleAfterSeconds = 300 }
        };
        var healthCheck = new HostedServicesHealthCheck(tracker, new TestOptionsMonitor<HealthCheckSettings>(settings));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_unhealthy_when_a_heartbeat_is_stale()
    {
        var tracker = new HostedServiceHealthTracker();
        tracker.ReportHeartbeat("note-sync-worker");
        // Waiting a few milliseconds guarantees some time has elapsed since the heartbeat above, so a
        // zero-second staleness window deterministically counts it as stale without a real 0-second race.
        await Task.Delay(10);
        var settings = new HealthCheckSettings
        {
            HostedServices = new HostedServicesHealthCheckSettings { Enabled = true, StaleAfterSeconds = 0 }
        };
        var healthCheck = new HostedServicesHealthCheck(tracker, new TestOptionsMonitor<HealthCheckSettings>(settings));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("note-sync-worker", result.Description);
    }
}
