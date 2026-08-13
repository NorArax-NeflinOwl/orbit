using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orbit.Api.HealthChecks;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.HealthChecks;

public sealed class DiskSpaceHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_returns_healthy_without_checking_disk_when_disabled()
    {
        var settings = new HealthCheckSettings { DiskSpace = new DiskSpaceHealthCheckSettings { Enabled = false } };
        var healthCheck = new DiskSpaceHealthCheck(CreateConfiguration(), new TestOptionsMonitor<HealthCheckSettings>(settings));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Check disabled by configuration.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_free_space_is_above_the_minimum()
    {
        var settings = new HealthCheckSettings
        {
            DiskSpace = new DiskSpaceHealthCheckSettings { Enabled = true, MinimumFreeBytes = 1 }
        };
        var healthCheck = new DiskSpaceHealthCheck(CreateConfiguration(), new TestOptionsMonitor<HealthCheckSettings>(settings));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_unhealthy_when_free_space_is_below_the_minimum()
    {
        // No real drive has this much free space, so the check is guaranteed to report unhealthy.
        var settings = new HealthCheckSettings
        {
            DiskSpace = new DiskSpaceHealthCheckSettings { Enabled = true, MinimumFreeBytes = long.MaxValue }
        };
        var healthCheck = new DiskSpaceHealthCheck(CreateConfiguration(), new TestOptionsMonitor<HealthCheckSettings>(settings));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static IConfiguration CreateConfiguration()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "orbit-disk-space-health-check-tests.db");

        return new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("ConnectionStrings:Orbit", $"Data Source={databasePath}")])
            .Build();
    }
}
